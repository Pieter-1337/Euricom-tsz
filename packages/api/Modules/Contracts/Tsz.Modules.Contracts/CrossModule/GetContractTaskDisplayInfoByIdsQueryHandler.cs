using Tsz.Infrastructure.Abstractions;
using Tsz.Modules.Contracts.Contracts.Queries;
using Tsz.Modules.Contracts.Domain.Contracts;
using Tsz.Modules.Customers.Contracts;
using Tsz.Modules.Customers.Contracts.Queries;

namespace Tsz.Modules.Contracts.CrossModule;

internal sealed class GetContractTaskDisplayInfoByIdsQueryHandler(
    IUnitOfWork uow,
    ICustomersAccessModule customers)
    : IQueryHandler<GetContractTaskDisplayInfoByIdsQuery, IReadOnlyList<ContractTaskDisplayInfoDto>>
{
    public async Task<IReadOnlyList<ContractTaskDisplayInfoDto>> HandleAsync(
        GetContractTaskDisplayInfoByIdsQuery query,
        CancellationToken ct = default)
    {
        if (query.ContractTaskIds.Count == 0)
            return [];

        var taskIds = query.ContractTaskIds.ToHashSet();

        var contracts = await uow.RepositoryFor<Contract>()
            .GetAllAsListAsync(
                filter: c => c.Tasks.Any(t => taskIds.Contains(t.Id)),
                ignoreQueryFilters: true,
                ct: ct);

        var customerIds = contracts.Select(c => c.CustomerId).Distinct().ToList();
        var customerNames = await customers.ExecuteQueryAsync(
            new GetCustomerNamesByIdsQuery(customerIds), ct);

        return contracts
            .SelectMany(c => c.Tasks
                .Where(t => taskIds.Contains(t.Id))
                .Select(t => new ContractTaskDisplayInfoDto(
                    t.Id,
                    t.Name,
                    c.Id,
                    c.Subject,
                    c.CustomerId,
                    customerNames.GetValueOrDefault(c.CustomerId, string.Empty))))
            .ToList();
    }
}
