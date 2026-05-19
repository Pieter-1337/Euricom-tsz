using System.Linq.Expressions;
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Modules.Users.Features;

public sealed record GetUsersQuery(UserRole? Role = null) : IQuery<IReadOnlyList<UserDto>>;

public sealed class GetUsersHandler(IUnitOfWork uow)
    : IQueryHandler<GetUsersQuery, IReadOnlyList<UserDto>>
{
    public async Task<IReadOnlyList<UserDto>> HandleAsync(GetUsersQuery query, CancellationToken ct = default)
    {
        Expression<Func<User, bool>>? filter = null;
        if (query.Role is not null)
        {
            var role = query.Role.Value;
            filter = u => u.RoleAssignments.Any(r => r.Role == role);
        }

        var users = await uow.RepositoryFor<User>().GetAllAsDtosAsync<UserDto>(filter, ct);
        return users.ToList();
    }
}
