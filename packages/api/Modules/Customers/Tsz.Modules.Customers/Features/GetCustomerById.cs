using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Auth;
using Tsz.Infrastructure.Auth.Validation;
using Tsz.Modules.Customers.Domain.Customers;

namespace Tsz.Modules.Customers.Features;

public sealed record GetCustomerByIdQuery(Guid Id) : IQuery<CustomerDto?>;

public sealed class GetCustomerByIdHandler(IUnitOfWork uow, IDataScopeAccessor scope)
    : IQueryHandler<GetCustomerByIdQuery, CustomerDto?>
{
    public async Task<CustomerDto?> HandleAsync(GetCustomerByIdQuery query, CancellationToken ct = default)
    {
        var filter = await ScopedFilter.ComposeAsync(scope, GetCustomersPagedHandler.ScopePolicy, c => c.Id == query.Id, ct);
        return await uow.RepositoryFor<Customer>().FirstOrDefaultAsDtoAsync<CustomerDto>(filter, ct);
    }
}
