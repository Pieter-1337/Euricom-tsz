using Microsoft.EntityFrameworkCore;
using Tsz.Infrastructure.Abstractions;
using Tsz.Modules.Customers.Contracts.Queries;
using Tsz.Modules.Customers.Domain.Customers;

namespace Tsz.Modules.Customers.CrossModule;

internal sealed class GetCustomerClientManagerIdQueryHandler(IUnitOfWork uow)
    : IQueryHandler<GetCustomerClientManagerIdQuery, Guid?>
{
    public Task<Guid?> HandleAsync(GetCustomerClientManagerIdQuery q, CancellationToken ct) =>
        uow.RepositoryFor<Customer>()
            .GetAll(c => c.Id == q.CustomerId)
            .Select(c => c.ClientManagerId)
            .FirstOrDefaultAsync(ct);
}
