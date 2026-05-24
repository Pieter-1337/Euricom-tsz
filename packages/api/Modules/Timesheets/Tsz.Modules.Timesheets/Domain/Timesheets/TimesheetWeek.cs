using FluentValidation.Results;
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Modules.Timesheets.Domain.Timesheets;

public class TimesheetWeek : IEntityBase
{
    public Guid Id { get; set; }
    public Guid UserId { get; private set; }
    public int IsoYear { get; private set; }
    public int IsoWeek { get; private set; }
    public TimesheetStatus Status { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    internal List<TimeEntry> TimeEntries { get; private set; } = [];

    public IReadOnlyCollection<TimeEntry> Entries => TimeEntries.AsReadOnly();

    private TimesheetWeek() { }

    public static TimesheetWeek Create(Guid userId, int isoYear, int isoWeek) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        IsoYear = isoYear,
        IsoWeek = isoWeek,
        Status = TimesheetStatus.Draft,
    };

    public void ApplyTimeEntries(IReadOnlyList<TimeEntryBookingDto> desired)
    {
        EnsureDraft();

        var existingByKey = TimeEntries
            .ToDictionary(e => (e.ContractTaskId, e.Date));

        var desiredKeys = desired
            .Select(d => (d.ContractTaskId, d.Date))
            .ToHashSet();

        foreach (var entry in TimeEntries.Where(e => !desiredKeys.Contains((e.ContractTaskId, e.Date))).ToList())
            TimeEntries.Remove(entry);

        foreach (var dto in desired)
        {
            var key = (dto.ContractTaskId, dto.Date);
            if (existingByKey.TryGetValue(key, out var existing))
                existing.Update(dto.DurationHours);
            else
                TimeEntries.Add(TimeEntry.Create(dto.ContractTaskId, dto.Date, dto.DurationHours));
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
