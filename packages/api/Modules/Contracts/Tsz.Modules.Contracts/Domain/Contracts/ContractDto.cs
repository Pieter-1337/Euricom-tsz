using System.Linq.Expressions;
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Modules.Contracts.Domain.Contracts;

public sealed record ContractDto(
    Guid Id,
    int Number,
    string Subject,
    Guid CustomerId,
    Guid? ClientManagerId,
    DateOnly Start,
    DateOnly? End,
    IReadOnlyCollection<ContractTaskDto> Tasks,
    IReadOnlyCollection<Guid> ConsultantIds)
    : IEntityDto<Contract, ContractDto>
{
    public static Expression<Func<Contract, ContractDto>> Project =>
        c => new ContractDto(
            c.Id,
            c.Number,
            c.Subject,
            c.CustomerId,
            c.ClientManagerId,
            c.Start,
            c.End,
            c.Tasks.Select(t => new ContractTaskDto(t.Id, t.Name, t.Rate, t.DeletedAt)).ToList(),
            c.Consultants.Select(x => x.UserId).ToList());

    public static ContractDto ToDto(Contract entity) =>
        new(
            entity.Id,
            entity.Number,
            entity.Subject,
            entity.CustomerId,
            entity.ClientManagerId,
            entity.Start,
            entity.End,
            entity.Tasks.Select(t => new ContractTaskDto(t.Id, t.Name, t.Rate, t.DeletedAt)).ToList(),
            entity.ConsultantIds);
}
