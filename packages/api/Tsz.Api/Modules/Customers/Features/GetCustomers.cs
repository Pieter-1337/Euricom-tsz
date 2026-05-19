using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Modules.Customers.Features;

public sealed record GetCustomersQuery : IQuery<IReadOnlyList<CustomerDto>>;

public sealed class GetCustomersHandler(IUnitOfWork uow)
    : IQueryHandler<GetCustomersQuery, IReadOnlyList<CustomerDto>>
{
    public async Task<IReadOnlyList<CustomerDto>> HandleAsync(GetCustomersQuery query, CancellationToken ct = default)
    {
        var customers = await uow.RepositoryFor<Customer>().GetAllAsDtosAsync<CustomerDto>(ct: ct);
        return customers.ToList();
    }
}
