using Microsoft.EntityFrameworkCore;
using Tsz.Infrastructure.Abstractions;
using Tsz.Modules.Customers.Contracts.Queries;
using Tsz.Modules.Customers.Domain.Customers;

namespace Tsz.Modules.Customers.CrossModule;

internal sealed class FindCustomerIdsBySearchQueryHandler(IUnitOfWork uow)
    : IQueryHandler<FindCustomerIdsBySearchQuery, IReadOnlyList<Guid>>
{
    public async Task<IReadOnlyList<Guid>> HandleAsync(FindCustomerIdsBySearchQuery q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q.Term))
            return Array.Empty<Guid>();

        var term = q.Term.ToLower();
        return await uow.RepositoryFor<Customer>()
            .GetAll(c => c.Name.ToLower().Contains(term))
            .Select(c => c.Id)
            .ToListAsync(ct);
    }
}
