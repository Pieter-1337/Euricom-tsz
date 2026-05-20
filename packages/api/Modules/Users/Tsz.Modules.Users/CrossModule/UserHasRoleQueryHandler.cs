using Tsz.Infrastructure.Abstractions;
using Tsz.Modules.Users.Contracts;
using Tsz.Modules.Users.Contracts.Queries;
using Tsz.Modules.Users.Domain.Users;

namespace Tsz.Modules.Users.CrossModule;

internal sealed class UserHasRoleQueryHandler(IUnitOfWork uow) : IQueryHandler<UserHasRoleQuery, bool>
{
    public Task<bool> HandleAsync(UserHasRoleQuery q, CancellationToken ct) =>
        uow.RepositoryFor<User>().ExistsAsync(
            u => u.Id == q.UserId && u.RoleAssignments.Any(r => r.Role == q.Role), ct);
}
