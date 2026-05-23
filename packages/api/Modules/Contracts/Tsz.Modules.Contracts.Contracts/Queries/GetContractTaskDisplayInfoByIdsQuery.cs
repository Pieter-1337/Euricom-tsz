using Tsz.Infrastructure.Abstractions;

namespace Tsz.Modules.Contracts.Contracts.Queries;

public sealed record ContractTaskDisplayInfoDto(
    Guid ContractTaskId,
    string TaskName,
    Guid ContractId,
    string ContractSubject,
    Guid CustomerId,
    string CustomerName);

public sealed record GetContractTaskDisplayInfoByIdsQuery(
    IReadOnlyList<Guid> ContractTaskIds)
    : IQuery<IReadOnlyList<ContractTaskDisplayInfoDto>>;
