using System.Linq.Expressions;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Common.Pagination;
using Tsz.Modules.Users.Contracts;
using Tsz.Modules.Users.Domain.Users;

namespace Tsz.Modules.Users.Features;

public record GetImpersonationTargetsQuery(
    string? Search,
    string? SortBy,
    SortDirection SortDir,
    int PageSize,
    string? Cursor)
    : KeysetQueryOptions(Search, SortBy, SortDir, PageSize, Cursor, DeletedOnly: false),
      IQuery<KeysetPage<UserDto>>;

public sealed class GetImpersonationTargetsQueryValidator
    : KeysetQueryOptionsValidator<GetImpersonationTargetsQuery, User>
{
    public GetImpersonationTargetsQueryValidator() : base(GetImpersonationTargetsHandler.Sort) { }
}

public sealed class GetImpersonationTargetsHandler(IUnitOfWork uow)
    : IQueryHandler<GetImpersonationTargetsQuery, KeysetPage<UserDto>>
{
    internal static readonly SortMap<User> Sort = new(
        new SortColumn<User>("name", (Expression<Func<User, string>>)(u => u.FirstName), typeof(string)),
        new SortColumn<User>("email", (Expression<Func<User, string>>)(u => u.Email), typeof(string)));

    private static readonly SearchableField<User>[] Searchable =
    [
        SearchableField<User>.Column(u => u.FirstName),
        SearchableField<User>.Column(u => u.LastName),
        SearchableField<User>.Column(u => u.Email),
    ];

    public Task<KeysetPage<UserDto>> HandleAsync(GetImpersonationTargetsQuery query, CancellationToken ct = default) =>
        uow.RepositoryFor<User>().GetPagedAsync(
            query,
            Sort,
            Searchable,
            UserDto.Project,
            filter: u => !u.RoleAssignments.Any(r => r.Role == UserRole.Admin),
            ct: ct,
            ignoreQueryFilters: false);
}
