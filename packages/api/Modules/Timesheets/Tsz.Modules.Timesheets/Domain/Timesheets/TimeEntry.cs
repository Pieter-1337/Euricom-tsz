namespace Tsz.Modules.Timesheets.Domain.Timesheets;

public class TimeEntry
{
    public Guid Id { get; private set; }
    public Guid ContractTaskId { get; private set; }
    public DateOnly Date { get; private set; }
    public decimal DurationHours { get; private set; }

    private TimeEntry() { }

    internal static TimeEntry Create(Guid contractTaskId, DateOnly date, decimal durationHours) => new()
    {
        Id = Guid.NewGuid(),
        ContractTaskId = contractTaskId,
        Date = date,
        DurationHours = durationHours,
    };

    internal void Update(decimal durationHours) => DurationHours = durationHours;
}
