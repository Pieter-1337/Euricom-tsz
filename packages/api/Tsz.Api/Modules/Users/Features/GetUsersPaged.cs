using System.Linq.Expressions;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Common.Pagination;

namespace Tsz.Api.Modules.Users.Features;

public record GetUsersPagedQuery(
    string? Search,
    string? SortBy,
    SortDirection SortDir,
    int PageSize,
    string? Cursor,
    bool IncludeDeleted)
    : KeysetQueryOptions(Search, SortBy, SortDir, PageSize, Cursor, IncludeDeleted),
      IQuery<KeysetPage<UserDto>>;

public sealed class GetUsersPagedQueryValidator
    : KeysetQueryOptionsValidator<GetUsersPagedQuery, User>
{
    public GetUsersPagedQueryValidator() : base(GetUsersPagedHandler.Sort) { }
}

public sealed class GetUsersPagedHandler(IUnitOfWork uow)
    : IQueryHandler<GetUsersPagedQuery, KeysetPage<UserDto>>
{
    // Sort by FirstName only — the current list column is "name" (first name).
    // A future slice can add LastName as a secondary key or rename the wire key to "firstName".
    internal static readonly SortMap<User> Sort = new(
        new SortColumn<User>("name", (Expression<Func<User, string>>)(u => u.FirstName), typeof(string)),
        new SortColumn<User>("email", (Expression<Func<User, string>>)(u => u.Email), typeof(string)),
        new SortColumn<User>("role", (Expression<Func<User, string>>)(u => u.Role.ToString()), typeof(string)));

    private static readonly Expression<Func<User, string>>[] Searchable =
    [
        u => u.FirstName,
        u => u.LastName,
        u => u.Email,
        u => u.Role.ToString(),
    ];

    public Task<KeysetPage<UserDto>> HandleAsync(GetUsersPagedQuery query, CancellationToken ct = default) =>
        uow.RepositoryFor<User>().GetPagedAsync(
            query,
            Sort,
            Searchable,
            UserDto.Project,
            ct: ct,
            ignoreQueryFilters: query.IncludeDeleted);
}
