using Tsz.Infrastructure.Abstractions;
using Tsz.Modules.Users.Contracts.Queries;
using Tsz.Modules.Users.Domain.Users;

namespace Tsz.Modules.Users.CrossModule;

internal sealed class UserExistsQueryHandler(IUnitOfWork uow) : IQueryHandler<UserExistsQuery, bool>
{
    public Task<bool> HandleAsync(UserExistsQuery q, CancellationToken ct) =>
        uow.RepositoryFor<User>().ExistsAsync(u => u.Id == q.UserId, ct);
}
