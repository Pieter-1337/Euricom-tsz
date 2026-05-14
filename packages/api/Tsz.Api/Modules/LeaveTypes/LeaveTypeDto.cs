using System.Linq.Expressions;
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Modules.LeaveTypes;

public sealed record LeaveTypeDto(
    Guid Id,
    string Name,
    string? PayrollCode,
    string? ReportingCode,
    string? Group,
    int? PrioInGroup,
    decimal? DefaultDays,
    LeaveAllowed DefaultAllowed)
    : IEntityDto<LeaveType, LeaveTypeDto>
{
    public static Expression<Func<LeaveType, LeaveTypeDto>> Project =>
        lt => new LeaveTypeDto(
            lt.Id,
            lt.Name,
            lt.PayrollCode,
            lt.ReportingCode,
            lt.Group,
            lt.PrioInGroup,
            lt.DefaultDays,
            lt.DefaultAllowed);

    public static LeaveTypeDto ToDto(LeaveType entity) =>
        new(entity.Id, entity.Name, entity.PayrollCode, entity.ReportingCode,
            entity.Group, entity.PrioInGroup, entity.DefaultDays, entity.DefaultAllowed);
}
