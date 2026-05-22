using System.Linq.Expressions;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Auth;
using Tsz.Infrastructure.Common.Pagination;
using Tsz.Modules.Customers.Domain.Customers;
using Tsz.Modules.Users.Contracts;

namespace Tsz.Modules.Customers.Features;

public record GetCustomersPagedQuery(
    string? Search,
    string? SortBy,
    SortDirection SortDir,
    int PageSize,
    string? Cursor,
    bool DeletedOnly)
    : KeysetQueryOptions(Search, SortBy, SortDir, PageSize, Cursor, DeletedOnly),
      IQuery<KeysetPage<CustomerDto>>;

public sealed class GetCustomersPagedQueryValidator
    : KeysetQueryOptionsValidator<GetCustomersPagedQuery, Customer>
{
    public GetCustomersPagedQueryValidator() : base(GetCustomersPagedHandler.Sort) { }
}

public sealed class GetCustomersPagedHandler(IUnitOfWork uow, IDataScopeAccessor scope)
    : IQueryHandler<GetCustomersPagedQuery, KeysetPage<CustomerDto>>
{
    internal static readonly OwnershipPolicy<Customer> ScopePolicy = new(
        OwnerIdSelector: c => c.ClientManagerId,
        FullAccessRoles: [nameof(UserRole.Admin)]);

    internal static readonly SortMap<Customer> Sort = new(
        new SortColumn<Customer>("number", (Expression<Func<Customer, int>>)(c => c.Number), typeof(int)),
        new SortColumn<Customer>("name", (Expression<Func<Customer, string>>)(c => c.Name), typeof(string)),
        new SortColumn<Customer>("city", (Expression<Func<Customer, string?>>)(c => c.Address.City), typeof(string)),
        new SortColumn<Customer>("contactEmail", (Expression<Func<Customer, string>>)(c => c.ContactPerson.Email), typeof(string)));

    private static readonly SearchableField<Customer>[] Searchable =
    [
        SearchableField<Customer>.Column(c => c.Name),
        SearchableField<Customer>.Column(c => c.ContactPerson.Email),
        SearchableField<Customer>.Column(c => c.ContactPerson.Name!),
        SearchableField<Customer>.Column(c => c.Address.City!),
    ];

    public async Task<KeysetPage<CustomerDto>> HandleAsync(GetCustomersPagedQuery query, CancellationToken ct = default)
    {
        var filter = await scope.OwnershipFilterAsync(ScopePolicy, ct);
        return await uow.RepositoryFor<Customer>().GetPagedAsync(
            query, Sort, Searchable, CustomerDto.Project, filter: filter, ct: ct, ignoreQueryFilters: false);
    }
}
