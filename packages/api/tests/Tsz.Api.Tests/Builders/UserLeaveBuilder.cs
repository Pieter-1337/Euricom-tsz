using Tsz.Api.Modules.Users;

namespace Tsz.Api.Tests.Builders;

public static class UserLeaveBuilder
{
    public static UserLeave Build(Guid? userId = null, Guid? leaveTypeId = null, decimal? totalDays = 5m) =>
        UserLeave.Create(
            userId: userId ?? Guid.NewGuid(),
            leaveTypeId: leaveTypeId ?? Guid.NewGuid(),
            totalDays: totalDays);

    public static UserLeave WithId(this UserLeave entity, Guid id)
    {
        entity.Id = id;
        return entity;
    }

    public static UserLeave WithTotalDays(this UserLeave entity, decimal? days)
    {
        entity.SetTotalDays(days);
        return entity;
    }
}
