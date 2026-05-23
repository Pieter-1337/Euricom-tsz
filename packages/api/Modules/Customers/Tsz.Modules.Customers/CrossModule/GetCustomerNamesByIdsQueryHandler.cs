using Microsoft.EntityFrameworkCore;
using Tsz.Infrastructure.Abstractions;
using Tsz.Modules.Customers.Contracts.Queries;
using Tsz.Modules.Customers.Domain.Customers;

namespace Tsz.Modules.Customers.CrossModule;

internal sealed class GetCustomerNamesByIdsQueryHandler(IUnitOfWork uow)
    : IQueryHandler<GetCustomerNamesByIdsQuery, IReadOnlyDictionary<Guid, string>>
{
    public async Task<IReadOnlyDictionary<Guid, string>> HandleAsync(
        GetCustomerNamesByIdsQuery q, CancellationToken ct)
    {
        if (q.CustomerIds.Count == 0)
            return new Dictionary<Guid, string>();

        var ids = q.CustomerIds;
        return await uow.RepositoryFor<Customer>()
            .GetAll(c => ids.Contains(c.Id), ignoreQueryFilters: true)
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);
    }
}
