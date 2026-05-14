using System.Linq.Expressions;
using Tsz.Api.Modules.LeaveTypes;
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Modules.Users;

public sealed record UserLeaveDto(
    Guid Id,
    Guid LeaveTypeId,
    string LeaveTypeName,
    LeaveAllowed DefaultAllowed,
    decimal? TotalDays)
    : IEntityDto<UserLeave, UserLeaveDto>
{
    public static Expression<Func<UserLeave, UserLeaveDto>> Project =>
        ul => new UserLeaveDto(
            ul.Id,
            ul.LeaveTypeId,
            string.Empty,
            LeaveAllowed.NotAllowed,
            ul.TotalDays);

    // Satisfies IEntityDto — leave type name/defaultAllowed are unavailable without a join; callers use ToDto(entity, name, defaultAllowed).
    public static UserLeaveDto ToDto(UserLeave entity) =>
        new(entity.Id, entity.LeaveTypeId, string.Empty, LeaveAllowed.NotAllowed, entity.TotalDays);

    public static UserLeaveDto ToDto(UserLeave entity, string leaveTypeName, LeaveAllowed defaultAllowed) =>
        new(entity.Id, entity.LeaveTypeId, leaveTypeName, defaultAllowed, entity.TotalDays);
}
