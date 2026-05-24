using Shouldly;
using Tsz.Api.Tests.Builders;
using Tsz.Modules.Timesheets.Domain.Timesheets;

namespace Tsz.Api.Tests.Modules.Timesheets.Domain;

public class TimesheetWeekTests
{
    private static readonly DateOnly Mon = new(2026, 5, 18);
    private static readonly DateOnly Tue = new(2026, 5, 19);
    private static readonly DateOnly Wed = new(2026, 5, 20);

    private static readonly Guid TaskA = Guid.NewGuid();
    private static readonly Guid TaskB = Guid.NewGuid();
    private static readonly Guid LeaveTypeA = Guid.NewGuid();
    private static readonly Guid LeaveTypeB = Guid.NewGuid();

    [Fact]
    public void Create_SetsStatusDraft()
    {
        var week = TimesheetWeekBuilder.Build();
        week.Status.ShouldBe(TimesheetStatus.Draft);
    }

    [Fact]
    public void Create_HasUniqueKey_UserId_IsoYear_IsoWeek()
    {
        var userId = Guid.NewGuid();
        var week = TimesheetWeek.Create(userId, 2026, 21);
        week.UserId.ShouldBe(userId);
        week.IsoYear.ShouldBe(2026);
        week.IsoWeek.ShouldBe(21);
    }

    [Fact]
    public void ApplyTimeEntries_Draft_AddsEntries()
    {
        var week = TimesheetWeekBuilder.Build();

        week.ApplyTimeEntries([
            new TimeEntryBookingDto(TaskA, Mon, 8.00m),
            new TimeEntryBookingDto(TaskB, Tue, 4.00m),
        ]);

        week.Entries.Count.ShouldBe(2);
        week.Entries.ShouldContain(e => e.ContractTaskId == TaskA && e.Date == Mon && e.DurationHours == 8.00m);
        week.Entries.ShouldContain(e => e.ContractTaskId == TaskB && e.Date == Tue && e.DurationHours == 4.00m);
    }

    [Fact]
    public void ApplyTimeEntries_UpdatesExistingEntry()
    {
        var week = TimesheetWeekBuilder.Build();
        week.ApplyTimeEntries([new TimeEntryBookingDto(TaskA, Mon, 8.00m)]);

        week.ApplyTimeEntries([new TimeEntryBookingDto(TaskA, Mon, 4.00m)]);

        week.Entries.Count.ShouldBe(1);
        week.Entries.Single().DurationHours.ShouldBe(4.00m);
    }

    [Fact]
    public void ApplyTimeEntries_RemovesEntryNotInDesired()
    {
        var week = TimesheetWeekBuilder.Build();
        week.ApplyTimeEntries([
            new TimeEntryBookingDto(TaskA, Mon, 8.00m),
            new TimeEntryBookingDto(TaskB, Tue, 4.00m),
        ]);

        week.ApplyTimeEntries([new TimeEntryBookingDto(TaskA, Mon, 8.00m)]);

        week.Entries.Count.ShouldBe(1);
        week.Entries.Single().ContractTaskId.ShouldBe(TaskA);
    }

    [Fact]
    public void ApplyTimeEntries_EmptyDesired_ClearsAllEntries()
    {
        var week = TimesheetWeekBuilder.Build();
        week.ApplyTimeEntries([new TimeEntryBookingDto(TaskA, Mon, 8.00m)]);

        week.ApplyTimeEntries([]);

        week.Entries.ShouldBeEmpty();
    }

    [Fact]
    public void ApplyTimeEntries_NonDraft_ThrowsValidationException()
    {
        var week = TimesheetWeekBuilder.Build();
        var statusField = typeof(TimesheetWeek).GetProperty("Status",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        statusField!.SetValue(week, TimesheetStatus.Submitted);

        Should.Throw<Tsz.Infrastructure.Errors.ValidationException>(() =>
            week.ApplyTimeEntries([new TimeEntryBookingDto(TaskA, Mon, 8.00m)]));
    }

    [Fact]
    public void ApplyTimeEntries_Approved_ThrowsValidationException()
    {
        var week = TimesheetWeekBuilder.Build();
        var statusField = typeof(TimesheetWeek).GetProperty("Status",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        statusField!.SetValue(week, TimesheetStatus.Approved);

        Should.Throw<Tsz.Infrastructure.Errors.ValidationException>(() =>
            week.ApplyTimeEntries([new TimeEntryBookingDto(TaskA, Mon, 8.00m)]));
    }

    [Fact]
    public void ApplyTimeEntries_SameKeyDifferentTask_TreatedAsDifferentEntry()
    {
        var week = TimesheetWeekBuilder.Build();
        week.ApplyTimeEntries([
            new TimeEntryBookingDto(TaskA, Mon, 8.00m),
            new TimeEntryBookingDto(TaskB, Mon, 4.00m),
        ]);

        week.Entries.Count.ShouldBe(2);
    }

    // --- ApplyLeaveBookings ---

    [Fact]
    public void ApplyLeaveBookings_Draft_AddsEntries()
    {
        var week = TimesheetWeekBuilder.Build();

        week.ApplyLeaveBookings([
            new LeaveBookingDto(LeaveTypeA, Mon, 8.00m),
            new LeaveBookingDto(LeaveTypeB, Tue, 4.00m),
        ]);

        week.LeaveEntries.Count.ShouldBe(2);
        week.LeaveEntries.ShouldContain(e => e.LeaveTypeId == LeaveTypeA && e.Date == Mon && e.DurationHours == 8.00m);
        week.LeaveEntries.ShouldContain(e => e.LeaveTypeId == LeaveTypeB && e.Date == Tue && e.DurationHours == 4.00m);
    }

    [Fact]
    public void ApplyLeaveBookings_UpdatesExistingEntry()
    {
        var week = TimesheetWeekBuilder.Build();
        week.ApplyLeaveBookings([new LeaveBookingDto(LeaveTypeA, Mon, 8.00m)]);

        week.ApplyLeaveBookings([new LeaveBookingDto(LeaveTypeA, Mon, 4.00m)]);

        week.LeaveEntries.Count.ShouldBe(1);
        week.LeaveEntries.Single().DurationHours.ShouldBe(4.00m);
    }

    [Fact]
    public void ApplyLeaveBookings_RemovesEntryNotInDesired()
    {
        var week = TimesheetWeekBuilder.Build();
        week.ApplyLeaveBookings([
            new LeaveBookingDto(LeaveTypeA, Mon, 8.00m),
            new LeaveBookingDto(LeaveTypeB, Tue, 4.00m),
        ]);

        week.ApplyLeaveBookings([new LeaveBookingDto(LeaveTypeA, Mon, 8.00m)]);

        week.LeaveEntries.Count.ShouldBe(1);
        week.LeaveEntries.Single().LeaveTypeId.ShouldBe(LeaveTypeA);
    }

    [Fact]
    public void ApplyLeaveBookings_EmptyDesired_ClearsAllEntries()
    {
        var week = TimesheetWeekBuilder.Build();
        week.ApplyLeaveBookings([new LeaveBookingDto(LeaveTypeA, Mon, 8.00m)]);

        week.ApplyLeaveBookings([]);

        week.LeaveEntries.ShouldBeEmpty();
    }

    [Fact]
    public void ApplyLeaveBookings_NonDraft_ThrowsValidationException()
    {
        var week = TimesheetWeekBuilder.Build();
        typeof(TimesheetWeek).GetProperty("Status",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)!
            .SetValue(week, TimesheetStatus.Submitted);

        Should.Throw<Tsz.Infrastructure.Errors.ValidationException>(() =>
            week.ApplyLeaveBookings([new LeaveBookingDto(LeaveTypeA, Mon, 8.00m)]));
    }

    // --- Submit ---

    [Fact]
    public void Submit_FromDraft_SetsStatusSubmitted()
    {
        var week = TimesheetWeekBuilder.Build();

        week.Submit();

        week.Status.ShouldBe(TimesheetStatus.Submitted);
    }

    [Fact]
    public void Submit_FromSubmitted_ThrowsValidationException()
    {
        var week = TimesheetWeekBuilder.Build();
        week.Submit();

        Should.Throw<Tsz.Infrastructure.Errors.ValidationException>(() => week.Submit());
    }

    [Fact]
    public void Submit_FromApproved_ThrowsValidationException()
    {
        var week = TimesheetWeekBuilder.Build();
        week.Submit();
        week.Approve();

        Should.Throw<Tsz.Infrastructure.Errors.ValidationException>(() => week.Submit());
    }

    // --- Approve ---

    [Fact]
    public void Approve_FromSubmitted_SetsStatusApproved()
    {
        var week = TimesheetWeekBuilder.Build();
        week.Submit();

        week.Approve();

        week.Status.ShouldBe(TimesheetStatus.Approved);
    }

    [Fact]
    public void Approve_FromDraft_ThrowsValidationException()
    {
        var week = TimesheetWeekBuilder.Build();

        Should.Throw<Tsz.Infrastructure.Errors.ValidationException>(() => week.Approve());
    }

    [Fact]
    public void Approve_FromApproved_ThrowsValidationException()
    {
        var week = TimesheetWeekBuilder.Build();
        week.Submit();
        week.Approve();

        Should.Throw<Tsz.Infrastructure.Errors.ValidationException>(() => week.Approve());
    }

    // --- Reopen ---

    [Fact]
    public void Reopen_FromSubmitted_SetsStatusDraft()
    {
        var week = TimesheetWeekBuilder.Build();
        week.Submit();

        week.Reopen();

        week.Status.ShouldBe(TimesheetStatus.Draft);
    }

    [Fact]
    public void Reopen_FromApproved_SetsStatusDraft()
    {
        var week = TimesheetWeekBuilder.Build();
        week.Submit();
        week.Approve();

        week.Reopen();

        week.Status.ShouldBe(TimesheetStatus.Draft);
    }

    [Fact]
    public void Reopen_FromDraft_ThrowsValidationException()
    {
        var week = TimesheetWeekBuilder.Build();

        Should.Throw<Tsz.Infrastructure.Errors.ValidationException>(() => week.Reopen());
    }

    [Fact]
    public void Reopen_AfterReopen_CanBeSubmittedAgain()
    {
        var week = TimesheetWeekBuilder.Build();
        week.Submit();
        week.Reopen();

        week.Submit();

        week.Status.ShouldBe(TimesheetStatus.Submitted);
    }
}
