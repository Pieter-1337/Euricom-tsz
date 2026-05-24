namespace Tsz.Modules.Timesheets.Domain.Timesheets;

public class LeaveBooking
{
    public Guid Id { get; private set; }
    public Guid LeaveTypeId { get; private set; }
    public DateOnly Date { get; private set; }
    public decimal DurationHours { get; private set; }

    private LeaveBooking() { }

    internal static LeaveBooking Create(Guid leaveTypeId, DateOnly date, decimal durationHours) => new()
    {
        Id = Guid.NewGuid(),
        LeaveTypeId = leaveTypeId,
        Date = date,
        DurationHours = durationHours,
    };

    internal void Update(decimal durationHours) => DurationHours = durationHours;
}
