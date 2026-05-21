using Tsz.Infrastructure.Abstractions;
using Tsz.Modules.Contracts.Contracts.Queries;
using Tsz.Modules.Contracts.Domain.Contracts;

namespace Tsz.Modules.Contracts.CrossModule;

internal sealed class IsUserReferencedAsClientManagerOnContractQueryHandler(IUnitOfWork uow)
    : IQueryHandler<IsUserReferencedAsClientManagerOnContractQuery, bool>
{
    public Task<bool> HandleAsync(IsUserReferencedAsClientManagerOnContractQuery q, CancellationToken ct) =>
        uow.RepositoryFor<Contract>().ExistsAsync(c => c.ClientManagerId == q.UserId, ct);
}
