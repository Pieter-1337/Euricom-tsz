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

    private static readonly IReadOnlyList<TimeEntryBookingDto> NoTimeEntries = [];
    private static readonly IReadOnlyList<LeaveBookingDto> NoLeaveBookings = [];

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

    // --- ApplyBookings: time entries ---

    [Fact]
    public void ApplyBookings_Draft_AddsTimeEntries()
    {
        var week = TimesheetWeekBuilder.Build();

        week.ApplyBookings(
            [
                new TimeEntryBookingDto(TaskA, Mon, 8.00m),
                new TimeEntryBookingDto(TaskB, Tue, 4.00m),
            ],
            NoLeaveBookings);

        week.Entries.Count.ShouldBe(2);
        week.Entries.ShouldContain(e => e.ContractTaskId == TaskA && e.Date == Mon && e.DurationHours == 8.00m);
        week.Entries.ShouldContain(e => e.ContractTaskId == TaskB && e.Date == Tue && e.DurationHours == 4.00m);
    }

    [Fact]
    public void ApplyBookings_UpdatesExistingTimeEntry()
    {
        var week = TimesheetWeekBuilder.Build();
        week.ApplyBookings([new TimeEntryBookingDto(TaskA, Mon, 8.00m)], NoLeaveBookings);

        week.ApplyBookings([new TimeEntryBookingDto(TaskA, Mon, 4.00m)], NoLeaveBookings);

        week.Entries.Count.ShouldBe(1);
        week.Entries.Single().DurationHours.ShouldBe(4.00m);
    }

    [Fact]
    public void ApplyBookings_RemovesTimeEntryNotInDesired()
    {
        var week = TimesheetWeekBuilder.Build();
        week.ApplyBookings(
            [
                new TimeEntryBookingDto(TaskA, Mon, 8.00m),
                new TimeEntryBookingDto(TaskB, Tue, 4.00m),
            ],
            NoLeaveBookings);

        week.ApplyBookings([new TimeEntryBookingDto(TaskA, Mon, 8.00m)], NoLeaveBookings);

        week.Entries.Count.ShouldBe(1);
        week.Entries.Single().ContractTaskId.ShouldBe(TaskA);
    }

    [Fact]
    public void ApplyBookings_EmptyDesired_ClearsAllTimeEntries()
    {
        var week = TimesheetWeekBuilder.Build();
        week.ApplyBookings([new TimeEntryBookingDto(TaskA, Mon, 8.00m)], NoLeaveBookings);

        week.ApplyBookings(NoTimeEntries, NoLeaveBookings);

        week.Entries.ShouldBeEmpty();
    }

    [Fact]
    public void ApplyBookings_NonDraft_ThrowsValidationException()
    {
        var week = TimesheetWeekBuilder.Build();
        SetStatus(week, TimesheetStatus.Submitted);

        Should.Throw<Tsz.Infrastructure.Errors.ValidationException>(() =>
            week.ApplyBookings([new TimeEntryBookingDto(TaskA, Mon, 8.00m)], NoLeaveBookings));
    }

    [Fact]
    public void ApplyBookings_Approved_ThrowsValidationException()
    {
        var week = TimesheetWeekBuilder.Build();
        SetStatus(week, TimesheetStatus.Approved);

        Should.Throw<Tsz.Infrastructure.Errors.ValidationException>(() =>
            week.ApplyBookings([new TimeEntryBookingDto(TaskA, Mon, 8.00m)], NoLeaveBookings));
    }

    [Fact]
    public void ApplyBookings_SameDateDifferentTask_TreatedAsDifferentEntry()
    {
        var week = TimesheetWeekBuilder.Build();
        week.ApplyBookings(
            [
                new TimeEntryBookingDto(TaskA, Mon, 4.00m),
                new TimeEntryBookingDto(TaskB, Mon, 4.00m),
            ],
            NoLeaveBookings);

        week.Entries.Count.ShouldBe(2);
    }

    // --- ApplyBookings: leave bookings ---

    [Fact]
    public void ApplyBookings_Draft_AddsLeaveBookings()
    {
        var week = TimesheetWeekBuilder.Build();

        week.ApplyBookings(
            NoTimeEntries,
            [
                new LeaveBookingDto(LeaveTypeA, Mon, 8.00m),
                new LeaveBookingDto(LeaveTypeB, Tue, 4.00m),
            ]);

        week.LeaveEntries.Count.ShouldBe(2);
        week.LeaveEntries.ShouldContain(e => e.LeaveTypeId == LeaveTypeA && e.Date == Mon && e.DurationHours == 8.00m);
        week.LeaveEntries.ShouldContain(e => e.LeaveTypeId == LeaveTypeB && e.Date == Tue && e.DurationHours == 4.00m);
    }

    [Fact]
    public void ApplyBookings_UpdatesExistingLeaveBooking()
    {
        var week = TimesheetWeekBuilder.Build();
        week.ApplyBookings(NoTimeEntries, [new LeaveBookingDto(LeaveTypeA, Mon, 8.00m)]);

        week.ApplyBookings(NoTimeEntries, [new LeaveBookingDto(LeaveTypeA, Mon, 4.00m)]);

        week.LeaveEntries.Count.ShouldBe(1);
        week.LeaveEntries.Single().DurationHours.ShouldBe(4.00m);
    }

    [Fact]
    public void ApplyBookings_RemovesLeaveBookingNotInDesired()
    {
        var week = TimesheetWeekBuilder.Build();
        week.ApplyBookings(
            NoTimeEntries,
            [
                new LeaveBookingDto(LeaveTypeA, Mon, 8.00m),
                new LeaveBookingDto(LeaveTypeB, Tue, 4.00m),
            ]);

        week.ApplyBookings(NoTimeEntries, [new LeaveBookingDto(LeaveTypeA, Mon, 8.00m)]);

        week.LeaveEntries.Count.ShouldBe(1);
        week.LeaveEntries.Single().LeaveTypeId.ShouldBe(LeaveTypeA);
    }

    [Fact]
    public void ApplyBookings_EmptyDesired_ClearsAllLeaveBookings()
    {
        var week = TimesheetWeekBuilder.Build();
        week.ApplyBookings(NoTimeEntries, [new LeaveBookingDto(LeaveTypeA, Mon, 8.00m)]);

        week.ApplyBookings(NoTimeEntries, NoLeaveBookings);

        week.LeaveEntries.ShouldBeEmpty();
    }

    [Fact]
    public void ApplyBookings_NonDraft_LeaveOnly_ThrowsValidationException()
    {
        var week = TimesheetWeekBuilder.Build();
        SetStatus(week, TimesheetStatus.Submitted);

        Should.Throw<Tsz.Infrastructure.Errors.ValidationException>(() =>
            week.ApplyBookings(NoTimeEntries, [new LeaveBookingDto(LeaveTypeA, Mon, 8.00m)]));
    }

    // --- ApplyBookings: day-capacity invariant ---

    [Fact]
    public void ApplyBookings_TimeOnly_OverCap_Throws()
    {
        var week = TimesheetWeekBuilder.Build();

        var ex = Should.Throw<Tsz.Infrastructure.Errors.ValidationException>(() =>
            week.ApplyBookings(
                [
                    new TimeEntryBookingDto(TaskA, Mon, 5.00m),
                    new TimeEntryBookingDto(TaskB, Mon, 4.00m),
                ],
                NoLeaveBookings));

        ex.Errors.ShouldContain(e =>
            ((TimesheetErrors)e.CustomState).Code == TimesheetErrors.DayCapacityExceeded.Code);
    }

    [Fact]
    public void ApplyBookings_LeaveOnly_OverCap_Throws()
    {
        var week = TimesheetWeekBuilder.Build();

        var ex = Should.Throw<Tsz.Infrastructure.Errors.ValidationException>(() =>
            week.ApplyBookings(
                NoTimeEntries,
                [
                    new LeaveBookingDto(LeaveTypeA, Mon, 6.00m),
                    new LeaveBookingDto(LeaveTypeB, Mon, 4.00m),
                ]));

        ex.Errors.ShouldContain(e =>
            ((TimesheetErrors)e.CustomState).Code == TimesheetErrors.DayCapacityExceeded.Code);
    }

    [Fact]
    public void ApplyBookings_TimePlusLeave_OverCap_Throws()
    {
        var week = TimesheetWeekBuilder.Build();

        var ex = Should.Throw<Tsz.Infrastructure.Errors.ValidationException>(() =>
            week.ApplyBookings(
                [new TimeEntryBookingDto(TaskA, Mon, 6.00m)],
                [new LeaveBookingDto(LeaveTypeA, Mon, 4.00m)]));

        ex.Errors.ShouldContain(e =>
            ((TimesheetErrors)e.CustomState).Code == TimesheetErrors.DayCapacityExceeded.Code);
    }

    [Fact]
    public void ApplyBookings_ExactlyAtCap_Allowed()
    {
        var week = TimesheetWeekBuilder.Build();

        week.ApplyBookings(
            [new TimeEntryBookingDto(TaskA, Mon, 4.00m)],
            [new LeaveBookingDto(LeaveTypeA, Mon, 4.00m)]);

        week.Entries.Count.ShouldBe(1);
        week.LeaveEntries.Count.ShouldBe(1);
    }

    [Fact]
    public void ApplyBookings_UnderCapIncludingUnderfilledDay_Allowed()
    {
        var week = TimesheetWeekBuilder.Build();

        week.ApplyBookings(
            [
                new TimeEntryBookingDto(TaskA, Mon, 2.00m), // underfilled
                new TimeEntryBookingDto(TaskB, Tue, 8.00m),
            ],
            NoLeaveBookings);

        week.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public void ApplyBookings_MultiDay_OnlyOneDayOverCap_BlocksWholeWeek()
    {
        var week = TimesheetWeekBuilder.Build();

        var ex = Should.Throw<Tsz.Infrastructure.Errors.ValidationException>(() =>
            week.ApplyBookings(
                [
                    new TimeEntryBookingDto(TaskA, Mon, 8.00m), // OK
                    new TimeEntryBookingDto(TaskA, Tue, 5.00m), // Tue total = 9 → over
                    new TimeEntryBookingDto(TaskB, Tue, 4.00m),
                    new TimeEntryBookingDto(TaskA, Wed, 4.00m), // OK
                ],
                NoLeaveBookings));

        ex.Errors.ShouldContain(e =>
            ((TimesheetErrors)e.CustomState).Code == TimesheetErrors.DayCapacityExceeded.Code);

        // None of the entries should have been applied (all-or-nothing)
        week.Entries.ShouldBeEmpty();
    }

    [Fact]
    public void ApplyBookings_OverCap_DoesNotPartiallyMutate()
    {
        var week = TimesheetWeekBuilder.Build();
        week.ApplyBookings([new TimeEntryBookingDto(TaskA, Mon, 8.00m)], NoLeaveBookings);

        Should.Throw<Tsz.Infrastructure.Errors.ValidationException>(() =>
            week.ApplyBookings(
                [
                    new TimeEntryBookingDto(TaskA, Mon, 5.00m),
                    new TimeEntryBookingDto(TaskB, Mon, 4.00m),
                ],
                NoLeaveBookings));

        // Original state preserved
        week.Entries.Count.ShouldBe(1);
        week.Entries.Single().DurationHours.ShouldBe(8.00m);
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

    private static void SetStatus(TimesheetWeek week, TimesheetStatus status)
    {
        typeof(TimesheetWeek)
            .GetProperty("Status", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)!
            .SetValue(week, status);
    }
}
