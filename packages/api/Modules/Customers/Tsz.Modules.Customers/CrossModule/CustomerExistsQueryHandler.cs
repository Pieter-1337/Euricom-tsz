using System.Linq.Expressions;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Auth;
using Tsz.Infrastructure.Common.Pagination;
using Tsz.Modules.Customers.Contracts.Queries;
using Tsz.Modules.Customers.Domain.Customers;
using Tsz.Modules.Customers.Features;

namespace Tsz.Modules.Customers.CrossModule;

internal sealed class CustomerExistsQueryHandler(IUnitOfWork uow, IDataScopeAccessor scope)
    : IQueryHandler<CustomerExistsQuery, bool>
{
    public async Task<bool> HandleAsync(CustomerExistsQuery q, CancellationToken ct)
    {
        var ownership = await scope.OwnershipFilterAsync(GetCustomersPagedHandler.ScopePolicy, ct);
        Expression<Func<Customer, bool>> filter = ownership is null
            ? c => c.Id == q.CustomerId
            : ownership.And(c => c.Id == q.CustomerId);
        return await uow.RepositoryFor<Customer>().ExistsAsync(filter, ct);
    }
}
