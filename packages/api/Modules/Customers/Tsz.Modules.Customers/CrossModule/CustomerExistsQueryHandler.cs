using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Auth;
using Tsz.Infrastructure.Auth.Validation;
using Tsz.Modules.Customers.Contracts.Queries;
using Tsz.Modules.Customers.Domain.Customers;
using Tsz.Modules.Customers.Features;

namespace Tsz.Modules.Customers.CrossModule;

internal sealed class CustomerExistsQueryHandler(IUnitOfWork uow, IDataScopeAccessor scope)
    : IQueryHandler<CustomerExistsQuery, bool>
{
    public async Task<bool> HandleAsync(CustomerExistsQuery q, CancellationToken ct)
    {
        var filter = await ScopedFilter.ComposeAsync(scope, GetCustomersPagedHandler.ScopePolicy, c => c.Id == q.CustomerId, ct);
        return await uow.RepositoryFor<Customer>().ExistsAsync(filter, ct);
    }
}
