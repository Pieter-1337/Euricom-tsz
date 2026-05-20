using Tsz.Infrastructure.Abstractions;
using Tsz.Modules.Customers.Contracts.Queries;
using Tsz.Modules.Customers.Domain.Customers;

namespace Tsz.Modules.Customers.CrossModule;

internal sealed class IsUserReferencedAsClientManagerQueryHandler(IUnitOfWork uow)
    : IQueryHandler<IsUserReferencedAsClientManagerQuery, bool>
{
    public Task<bool> HandleAsync(IsUserReferencedAsClientManagerQuery q, CancellationToken ct) =>
        uow.RepositoryFor<Customer>().ExistsAsync(c => c.ClientManagerId == q.UserId, ct);
}
