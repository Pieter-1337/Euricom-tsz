using Tsz.Infrastructure.Abstractions;
using Tsz.Modules.Contracts.Contracts;
using Tsz.Modules.Contracts.Contracts.Queries;

namespace Tsz.Modules.Timesheets.Features;

public sealed record GetSelectableContractTasksQuery(
    Guid UserId,
    int IsoYear,
    int IsoWeek)
    : IQuery<IReadOnlyList<SelectableContractTaskDto>>;

public sealed class GetSelectableContractTasksHandler(IContractsAccessModule contracts)
    : IQueryHandler<GetSelectableContractTasksQuery, IReadOnlyList<SelectableContractTaskDto>>
{
    public Task<IReadOnlyList<SelectableContractTaskDto>> HandleAsync(
        GetSelectableContractTasksQuery query,
        CancellationToken ct = default) =>
        contracts.ExecuteQueryAsync(
            new GetSelectableContractTasksForConsultantInWeekQuery(
                query.UserId, query.IsoYear, query.IsoWeek),
            ct);
}
