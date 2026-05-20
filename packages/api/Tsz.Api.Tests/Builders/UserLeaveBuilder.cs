using Tsz.Modules.Users.Domain.Leaves;

namespace Tsz.Api.Tests.Builders;

public static class UserLeaveBuilder
{
    public static UserLeave Build(Guid? userId = null, Guid? leaveTypeId = null, int? year = null, decimal? totalDays = 5m) =>
        UserLeave.Create(
            userId: userId ?? Guid.NewGuid(),
            leaveTypeId: leaveTypeId ?? Guid.NewGuid(),
            year: year ?? DateTimeOffset.UtcNow.Year,
            totalDays: totalDays);

    public static UserLeave ForUser(this UserLeave entity, Guid userId)
    {
        entity.Id = entity.Id;
        return UserLeave.Create(userId, entity.LeaveTypeId, entity.Year, entity.TotalDays).WithId(entity.Id);
    }

    public static UserLeave ForType(this UserLeave entity, Guid leaveTypeId) =>
        UserLeave.Create(entity.UserId, leaveTypeId, entity.Year, entity.TotalDays).WithId(entity.Id);

    public static UserLeave Year(this UserLeave entity, int year) =>
        UserLeave.Create(entity.UserId, entity.LeaveTypeId, year, entity.TotalDays).WithId(entity.Id);

    public static UserLeave WithTotal(this UserLeave entity, decimal? days)
    {
        entity.SetTotalDays(days);
        return entity;
    }

    public static UserLeave WithId(this UserLeave entity, Guid id)
    {
        entity.Id = id;
        return entity;
    }
}
