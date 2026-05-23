using Tsz.Infrastructure.Abstractions;

namespace Tsz.Modules.Contracts.Contracts.Queries;

public sealed record SelectableContractTaskDto(
    Guid ContractTaskId,
    string TaskName,
    Guid ContractId,
    string ContractSubject,
    Guid CustomerId);

public sealed record GetSelectableContractTasksForConsultantInWeekQuery(
    Guid UserId,
    int IsoYear,
    int IsoWeek)
    : IQuery<IReadOnlyList<SelectableContractTaskDto>>;
