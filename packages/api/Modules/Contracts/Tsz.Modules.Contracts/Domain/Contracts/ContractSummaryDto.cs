using System.Linq.Expressions;

namespace Tsz.Modules.Contracts.Domain.Contracts;

public sealed record ContractSummaryDto(
    Guid Id,
    int Number,
    string Subject,
    Guid CustomerId,
    DateOnly Start,
    DateOnly? End,
    int ActiveTaskCount,
    int ConsultantCount)
{
    public static Expression<Func<Contract, ContractSummaryDto>> Project =>
        c => new ContractSummaryDto(
            c.Id,
            c.Number,
            c.Subject,
            c.CustomerId,
            c.Start,
            c.End,
            c.Tasks.Count(t => t.DeletedAt == null),
            c.Consultants.Count);
}
