using FluentValidation.Results;
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Modules.Timesheets.Domain.Timesheets;

public class TimesheetWeek : IEntityBase
{
    /// <summary>
    /// A consultant's daily working-hours capacity. v1 constant for everyone.
    /// Drives both the per-day booking cap and the leave-day-equivalence arithmetic
    /// (1 leave day = WorkdayCapacity hours). Forward-compat to per-user
    /// <c>User.WorkingPatternHoursPerDay</c> when part-time consultants land.
    /// </summary>
    public const decimal WorkdayCapacity = 8.00m;

    public Guid Id { get; set; }
    public Guid UserId { get; private set; }
    public int IsoYear { get; private set; }
    public int IsoWeek { get; private set; }
    public TimesheetStatus Status { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    internal List<TimeEntry> TimeEntries { get; private set; } = [];
    internal List<LeaveBooking> LeaveBookings { get; private set; } = [];

    public IReadOnlyCollection<TimeEntry> Entries => TimeEntries.AsReadOnly();
    public IReadOnlyCollection<LeaveBooking> LeaveEntries => LeaveBookings.AsReadOnly();

    private TimesheetWeek() { }

    public static TimesheetWeek Create(Guid userId, int isoYear, int isoWeek) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        IsoYear = isoYear,
        IsoWeek = isoWeek,
        Status = TimesheetStatus.Draft,
    };

    /// <summary>
    /// Replaces the week's time entries and leave bookings with the desired post-state,
    /// enforcing the day-capacity invariant (Σ DurationHours per Date ≤ WorkdayCapacity)
    /// against the combined post-state. Either both lists apply or none do.
    /// </summary>
    public void ApplyBookings(
        IReadOnlyList<TimeEntryBookingDto> desiredTimeEntries,
        IReadOnlyList<LeaveBookingDto> desiredLeaveBookings)
    {
        EnsureDraft();
        EnsureDayCapacity(desiredTimeEntries, desiredLeaveBookings);

        SyncTimeEntries(desiredTimeEntries);
        SyncLeaveBookings(desiredLeaveBookings);
    }

    private static void EnsureDayCapacity(
        IReadOnlyList<TimeEntryBookingDto> timeEntries,
        IReadOnlyList<LeaveBookingDto> leaveBookings)
    {
        var anyOverCap = timeEntries.Select(e => (e.Date, e.DurationHours))
            .Concat(leaveBookings.Select(b => (b.Date, b.DurationHours)))
            .GroupBy(x => x.Date)
            .Any(g => g.Sum(x => x.DurationHours) > WorkdayCapacity);

        if (anyOverCap)
            throw new Tsz.Infrastructure.Errors.ValidationException(
                [new ValidationFailure(string.Empty, TimesheetErrors.DayCapacityExceeded.Message)
                    { CustomState = TimesheetErrors.DayCapacityExceeded }]);
    }

    private void SyncTimeEntries(IReadOnlyList<TimeEntryBookingDto> desired)
    {
        var existingByKey = TimeEntries.ToDictionary(e => (e.ContractTaskId, e.Date));
        var desiredKeys = desired.Select(d => (d.ContractTaskId, d.Date)).ToHashSet();

        foreach (var entry in TimeEntries.Where(e => !desiredKeys.Contains((e.ContractTaskId, e.Date))).ToList())
            TimeEntries.Remove(entry);

        foreach (var dto in desired)
        {
            if (existingByKey.TryGetValue((dto.ContractTaskId, dto.Date), out var existing))
                existing.Update(dto.DurationHours);
            else
                TimeEntries.Add(TimeEntry.Create(dto.ContractTaskId, dto.Date, dto.DurationHours));
        }
    }

    private void SyncLeaveBookings(IReadOnlyList<LeaveBookingDto> desired)
    {
        var existingByKey = LeaveBookings.ToDictionary(e => (e.LeaveTypeId, e.Date));
        var desiredKeys = desired.Select(d => (d.LeaveTypeId, d.Date)).ToHashSet();

        foreach (var entry in LeaveBookings.Where(e => !desiredKeys.Contains((e.LeaveTypeId, e.Date))).ToList())
            LeaveBookings.Remove(entry);

        foreach (var dto in desired)
        {
            if (existingByKey.TryGetValue((dto.LeaveTypeId, dto.Date), out var existing))
                existing.Update(dto.DurationHours);
            else
                LeaveBookings.Add(LeaveBooking.Create(dto.LeaveTypeId, dto.Date, dto.DurationHours));
        }
    }

    public void Submit()
    {
        if (Status != TimesheetStatus.Draft)
            throw new Tsz.Infrastructure.Errors.ValidationException(
                [new ValidationFailure(string.Empty, TimesheetErrors.NotSubmittable.Message)
                    { CustomState = TimesheetErrors.NotSubmittable }]);
        Status = TimesheetStatus.Submitted;
    }

    public void Approve()
    {
        if (Status != TimesheetStatus.Submitted)
            throw new Tsz.Infrastructure.Errors.ValidationException(
                [new ValidationFailure(string.Empty, TimesheetErrors.NotApprovable.Message)
                    { CustomState = TimesheetErrors.NotApprovable }]);
        Status = TimesheetStatus.Approved;
    }

    public void Reopen()
    {
        if (Status != TimesheetStatus.Submitted && Status != TimesheetStatus.Approved)
            throw new Tsz.Infrastructure.Errors.ValidationException(
                [new ValidationFailure(string.Empty, TimesheetErrors.NotReopenable.Message)
                    { CustomState = TimesheetErrors.NotReopenable }]);
        Status = TimesheetStatus.Draft;
    }

    private void EnsureDraft()
    {
        if (Status != TimesheetStatus.Draft)
            throw new Tsz.Infrastructure.Errors.ValidationException(
                [new ValidationFailure(string.Empty, TimesheetErrors.NotDraft.Message)
                    { CustomState = TimesheetErrors.NotDraft }]);
    }
}

public sealed record TimeEntryBookingDto(
    Guid ContractTaskId,
    DateOnly Date,
    decimal DurationHours);

public sealed record LeaveBookingDto(
    Guid LeaveTypeId,
    DateOnly Date,
    decimal DurationHours);
