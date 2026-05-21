namespace Tsz.Modules.Contracts.Domain.Contracts;

public sealed record ContractTaskDto(
    Guid Id,
    string Name,
    decimal Rate,
    DateTimeOffset? DeletedAt);
