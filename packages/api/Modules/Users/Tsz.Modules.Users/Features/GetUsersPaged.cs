using System.Linq.Expressions;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Common.Pagination;
using Tsz.Modules.Users.Contracts;
using Tsz.Modules.Users.Domain.Users;

namespace Tsz.Modules.Users.Features;

public record GetUsersPagedQuery(
    string? Search,
    string? SortBy,
    SortDirection SortDir,
    int PageSize,
    string? Cursor,
    bool DeletedOnly)
    : KeysetQueryOptions(Search, SortBy, SortDir, PageSize, Cursor, DeletedOnly),
      IQuery<KeysetPage<UserDto>>;

public sealed class GetUsersPagedQueryValidator
    : KeysetQueryOptionsValidator<GetUsersPagedQuery, User>
{
    public GetUsersPagedQueryValidator() : base(GetUsersPagedHandler.Sort) { }
}

public sealed class GetUsersPagedHandler(IUnitOfWork uow)
    : IQueryHandler<GetUsersPagedQuery, KeysetPage<UserDto>>
{
    internal static readonly SortMap<User> Sort = new(
        new SortColumn<User>("name", (Expression<Func<User, string>>)(u => u.FirstName), typeof(string)),
        new SortColumn<User>("email", (Expression<Func<User, string>>)(u => u.Email), typeof(string)));

    private static readonly SearchableField<User>[] Searchable =
    [
        SearchableField<User>.Column(u => u.FirstName),
        SearchableField<User>.Column(u => u.LastName),
        SearchableField<User>.Column(u => u.Email),
        SearchableField<User>.Column<UserRole>(u => u.RoleAssignments.Select(r => r.Role)),
    ];

    public Task<KeysetPage<UserDto>> HandleAsync(GetUsersPagedQuery query, CancellationToken ct = default) =>
        uow.RepositoryFor<User>().GetPagedAsync(query, Sort, Searchable, UserDto.Project, ct: ct, ignoreQueryFilters: false);
}
