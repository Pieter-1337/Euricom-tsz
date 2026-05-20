using Tsz.Infrastructure.Abstractions;

namespace Tsz.Modules.Users.Domain.Leaves;

public class UserLeave : IEntityBase
{
    public Guid Id { get; set; }
    public Guid UserId { get; private set; }
    public Guid LeaveTypeId { get; private set; }
    public int Year { get; private set; }
    public decimal? TotalDays { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    private UserLeave() { }

    public static UserLeave Create(Guid userId, Guid leaveTypeId, int year, decimal? totalDays) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        LeaveTypeId = leaveTypeId,
        Year = year,
        TotalDays = totalDays,
    };

    public void SetTotalDays(decimal? totalDays) => TotalDays = totalDays;
}
