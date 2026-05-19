using System.Linq.Expressions;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Common.Pagination;

namespace Tsz.Api.Modules.Customers.Features;

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

public sealed class GetCustomersPagedHandler(IUnitOfWork uow)
    : IQueryHandler<GetCustomersPagedQuery, KeysetPage<CustomerDto>>
{
    internal static readonly SortMap<Customer> Sort = new(
        new SortColumn<Customer>("number", (Expression<Func<Customer, int>>)(c => c.Number), typeof(int)),
        new SortColumn<Customer>("name", (Expression<Func<Customer, string>>)(c => c.Name), typeof(string)),
        new SortColumn<Customer>("city", (Expression<Func<Customer, string?>>)(c => c.Address.City), typeof(string)),
        new SortColumn<Customer>("contactEmail", (Expression<Func<Customer, string>>)(c => c.ContactPerson.Email), typeof(string)));

    private static readonly Expression<Func<Customer, string>>[] Searchable =
    [
        c => c.Name,
        c => c.ContactPerson.Email,
        c => c.ContactPerson.Name!,
        c => c.Address.City!,
    ];

    public Task<KeysetPage<CustomerDto>> HandleAsync(GetCustomersPagedQuery query, CancellationToken ct = default) =>
        uow.RepositoryFor<Customer>().GetPagedAsync(
            query,
            Sort,
            Searchable,
            CustomerDto.Project,
            filter: query.DeletedOnly ? c => c.DeletedAt != null : null,
            ct: ct,
            ignoreQueryFilters: query.DeletedOnly);
}
