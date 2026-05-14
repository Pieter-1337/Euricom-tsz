using Tsz.Api.Modules.LeaveTypes;

namespace Tsz.Api.Tests.Builders;

public static class LeaveTypeBuilder
{
    public static LeaveType Build(string? name = null, LeaveAllowed allowed = LeaveAllowed.Limited, decimal? days = 5m) =>
        LeaveType.Create(
            name: name ?? "LeaveType_" + Guid.NewGuid().ToString()[..8],
            defaultAllowed: allowed,
            defaultDays: days);

    public static LeaveType WithId(this LeaveType entity, Guid id)
    {
        entity.Id = id;
        return entity;
    }
}
