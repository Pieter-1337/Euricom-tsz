using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Modules.Users;

public class UserLeave : IEntityBase
{
    public Guid Id { get; set; }
    public Guid UserId { get; private set; }
    public Guid LeaveTypeId { get; private set; }
    public decimal? TotalDays { get; private set; }

    private UserLeave() { }

    public static UserLeave Create(Guid userId, Guid leaveTypeId, decimal? totalDays) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        LeaveTypeId = leaveTypeId,
        TotalDays = totalDays,
    };

    public void SetTotalDays(decimal? totalDays) => TotalDays = totalDays;
}
