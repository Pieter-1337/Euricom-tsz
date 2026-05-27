using Microsoft.EntityFrameworkCore;
using Tsz.Infrastructure.Abstractions;
using Tsz.Modules.Users.Contracts.Queries;
using Tsz.Modules.Users.Domain.Users;

namespace Tsz.Modules.Users.CrossModule;

internal sealed class GetUserNamesByIdsQueryHandler(IUnitOfWork uow)
    : IQueryHandler<GetUserNamesByIdsQuery, IReadOnlyDictionary<Guid, string>>
{
    public async Task<IReadOnlyDictionary<Guid, string>> HandleAsync(
        GetUserNamesByIdsQuery q, CancellationToken ct)
    {
        if (q.UserIds.Count == 0)
            return new Dictionary<Guid, string>();

        var ids = q.UserIds;
        return await uow.RepositoryFor<User>()
            .GetAll(u => ids.Contains(u.Id), ignoreQueryFilters: true)
            .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}", ct);
    }
}
