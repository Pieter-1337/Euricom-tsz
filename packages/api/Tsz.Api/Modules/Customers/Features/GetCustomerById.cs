using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Modules.Customers.Features;

public sealed record GetCustomerByIdQuery(Guid Id) : IQuery<CustomerDto?>;

public sealed class GetCustomerByIdHandler(IUnitOfWork uow)
    : IQueryHandler<GetCustomerByIdQuery, CustomerDto?>
{
    public Task<CustomerDto?> HandleAsync(GetCustomerByIdQuery query, CancellationToken ct = default) =>
        uow.RepositoryFor<Customer>()
            .FirstOrDefaultAsDtoAsync<CustomerDto>(c => c.Id == query.Id, ct);
}
