using System.Linq.Expressions;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Auth;
using Tsz.Infrastructure.Common.Pagination;
using Tsz.Modules.Customers.Domain.Customers;

namespace Tsz.Modules.Customers.Features;

public sealed record GetCustomerByIdQuery(Guid Id) : IQuery<CustomerDto?>;

public sealed class GetCustomerByIdHandler(IUnitOfWork uow, IDataScopeAccessor scope)
    : IQueryHandler<GetCustomerByIdQuery, CustomerDto?>
{
    public async Task<CustomerDto?> HandleAsync(GetCustomerByIdQuery query, CancellationToken ct = default)
    {
        var ownership = await scope.OwnershipFilterAsync(GetCustomersPagedHandler.ScopePolicy, ct);
        Expression<Func<Customer, bool>> filter = ownership is null
            ? c => c.Id == query.Id
            : ownership.And(c => c.Id == query.Id);

        return await uow.RepositoryFor<Customer>()
            .FirstOrDefaultAsDtoAsync<CustomerDto>(filter, ct);
    }
}
