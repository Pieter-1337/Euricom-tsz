using System.Linq.Expressions;
using Tsz.Infrastructure.Abstractions;
using Tsz.Modules.LeaveTypes.Contracts;

namespace Tsz.Modules.LeaveTypes.Domain.Leaves;

public sealed record UserLeaveDto(
    Guid Id,
    Guid LeaveTypeId,
    string LeaveTypeName,
    LeaveAllowed DefaultAllowed,
    int Year,
    decimal? TotalDays,
    decimal? TakenDays,
    decimal? BalanceDays)
    : IEntityDto<UserLeave, UserLeaveDto>
{
    public static Expression<Func<UserLeave, UserLeaveDto>> Project =>
        ul => new UserLeaveDto(
            ul.Id,
            ul.LeaveTypeId,
            string.Empty,
            LeaveAllowed.NotAllowed,
            ul.Year,
            ul.TotalDays,
            null,
            null);

    public static UserLeaveDto ToDto(UserLeave entity) =>
        new(entity.Id, entity.LeaveTypeId, string.Empty, LeaveAllowed.NotAllowed, entity.Year, entity.TotalDays, null, null);

    public static UserLeaveDto ToDto(UserLeave entity, string leaveTypeName, LeaveAllowed defaultAllowed) =>
        new(entity.Id, entity.LeaveTypeId, leaveTypeName, defaultAllowed, entity.Year, entity.TotalDays, null, null);
}
