using Tsz.Api.Modules.Users;

namespace Tsz.Api.Tests.Builders;

public static class LeaveTypeBuilder
{
    public static LeaveType WithName(string name) =>
        LeaveType.Create(name, LeaveAllowed.Limited, 5m);

    public static LeaveType Limited(string name, decimal days) =>
        LeaveType.Create(name, LeaveAllowed.Limited, days);

    public static LeaveType Unlimited(string name) =>
        LeaveType.Create(name, LeaveAllowed.Unlimited, null);

    public static LeaveType WithId(this LeaveType entity, Guid id)
    {
        entity.Id = id;
        return entity;
    }
}
