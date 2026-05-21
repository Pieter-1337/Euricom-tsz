using Tsz.Infrastructure.Abstractions;
using Tsz.Modules.Customers.Contracts.Queries;
using Tsz.Modules.Customers.Domain.Customers;

namespace Tsz.Modules.Customers.CrossModule;

internal sealed class CustomerExistsQueryHandler(IUnitOfWork uow)
    : IQueryHandler<CustomerExistsQuery, bool>
{
    public Task<bool> HandleAsync(CustomerExistsQuery q, CancellationToken ct) =>
        uow.RepositoryFor<Customer>().ExistsAsync(c => c.Id == q.CustomerId, ct);
}
