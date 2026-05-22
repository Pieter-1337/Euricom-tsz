using System.Linq.Expressions;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Auth;
using Tsz.Infrastructure.Common.Pagination;
using Tsz.Modules.Contracts.Domain.Contracts;
using Tsz.Modules.Customers.Contracts;
using Tsz.Modules.Customers.Contracts.Queries;
using Tsz.Modules.Users.Contracts;

namespace Tsz.Modules.Contracts.Features;

public sealed record GetContractsPagedQuery(
    string? Search,
    string? SortBy,
    SortDirection SortDir,
    int PageSize,
    string? Cursor,
    bool DeletedOnly,
    DateOnly? ActiveOnDate,
    Guid? CustomerId)
    : KeysetQueryOptions(Search, SortBy, SortDir, PageSize, Cursor, DeletedOnly),
      IQuery<KeysetPage<ContractSummaryDto>>;

public sealed class GetContractsPagedQueryValidator
    : KeysetQueryOptionsValidator<GetContractsPagedQuery, Contract>
{
    public GetContractsPagedQueryValidator() : base(GetContractsPagedHandler.Sort) { }
}

public sealed class GetContractsPagedHandler(
    IUnitOfWork uow,
    ICustomersAccessModule customers,
    IDataScopeAccessor scope)
    : IQueryHandler<GetContractsPagedQuery, KeysetPage<ContractSummaryDto>>
{
    internal static readonly OwnershipPolicy<Contract> ScopePolicy = new(
        OwnerIdSelector: c => c.ClientManagerId,
        FullAccessRoles: [nameof(UserRole.Admin)]);

    internal static readonly SortMap<Contract> Sort = new(
        new SortColumn<Contract>("number", (Expression<Func<Contract, int>>)(c => c.Number), typeof(int)),
        new SortColumn<Contract>("subject", (Expression<Func<Contract, string>>)(c => c.Subject), typeof(string)),
        new SortColumn<Contract>("start", (Expression<Func<Contract, DateOnly>>)(c => c.Start), typeof(DateOnly)));

    public async Task<KeysetPage<ContractSummaryDto>> HandleAsync(GetContractsPagedQuery query, CancellationToken ct = default)
    {
        var ownership = await scope.OwnershipFilterAsync(ScopePolicy, ct);
        var filter = await BuildFilterAsync(query, ownership, ct);
        return await uow.RepositoryFor<Contract>().GetPagedAsync(
            query,
            Sort,
            searchableFields: [],
            ContractSummaryDto.Project,
            filter: filter,
            ct: ct);
    }

    private async Task<Expression<Func<Contract, bool>>?> BuildFilterAsync(
        GetContractsPagedQuery query,
        Expression<Func<Contract, bool>>? ownership,
        CancellationToken ct)
    {
        var predicate = PredicateBuilder.BaseAnd<Contract>();
        var touched = false;

        if (ownership is not null)
        {
            predicate = predicate.And(ownership);
            touched = true;
        }

        if (query.CustomerId is { } customerId)
        {
            predicate = predicate.And(c => c.CustomerId == customerId);
            touched = true;
        }

        if (query.ActiveOnDate is { } activeOn)
        {
            predicate = predicate.And(c => c.Start <= activeOn && (c.End == null || c.End >= activeOn));
            touched = true;
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.ToLower();
            var matchedCustomerIds = await customers.ExecuteQueryAsync(new FindCustomerIdsBySearchQuery(term), ct);
            Expression<Func<Contract, bool>> searchPredicate = matchedCustomerIds.Count == 0
                ? c => c.Subject.ToLower().Contains(term)
                : c => c.Subject.ToLower().Contains(term) || matchedCustomerIds.Contains(c.CustomerId);
            predicate = predicate.And(searchPredicate);
            touched = true;
        }

        return touched ? predicate : null;
    }
}
