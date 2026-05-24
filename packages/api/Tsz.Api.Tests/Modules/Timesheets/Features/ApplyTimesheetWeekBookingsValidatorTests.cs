using Moq;
using Shouldly;
using Tsz.Modules.Contracts.Contracts;
using Tsz.Modules.Contracts.Contracts.Queries;
using Tsz.Modules.LeaveTypes.Contracts;
using Tsz.Modules.Timesheets.Domain.Timesheets;
using Tsz.Modules.Timesheets.Features;
using Tsz.Modules.Workdays.Contracts;
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Tests.Modules.Timesheets.Features;

public class ApplyTimesheetWeekBookingsValidatorTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid TaskId = Guid.NewGuid();
    private static readonly Guid LeaveTypeId = Guid.NewGuid();
    private const int Year = 2026;
    private const int Week = 21;

    // Mon 18 May 2026 .. Sun 24 May 2026
    private static readonly DateOnly Mon = new(2026, 5, 18);
    private static readonly DateOnly Tue = new(2026, 5, 19);
    private static readonly DateOnly Sat = new(2026, 5, 23);

    private static ApplyTimesheetWeekBookingsValidator BuildValidator(
        TimesheetWeek? existingWeek = null,
        bool isBusinessDay = true,
        bool taskEligible = true,
        bool leaveTypeExists = true,
        decimal? leaveAllowanceDays = null,
        IEnumerable<TimesheetWeek>? otherWeeks = null)
    {
        var uow = new Mock<IUnitOfWork>();
        var repo = new Mock<IRepository<TimesheetWeek>>();
        repo.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<TimesheetWeek, bool>>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
            .ReturnsAsync(existingWeek);
        repo.Setup(r => r.GetAllAsListAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<TimesheetWeek, bool>>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
            .ReturnsAsync((IEnumerable<TimesheetWeek>)(otherWeeks ?? []));
        uow.Setup(u => u.RepositoryFor<TimesheetWeek>()).Returns(repo.Object);

        var workdays = new Mock<IWorkdaysAccessModule>();
        workdays.Setup(w => w.IsBusinessDay(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(isBusinessDay);

        var contracts = new Mock<IContractsAccessModule>();
        var eligible = taskEligible
            ? (IReadOnlyList<SelectableContractTaskDto>)[new SelectableContractTaskDto(TaskId, "Task A", Guid.NewGuid(), "Contract", Guid.NewGuid())]
            : [];
        contracts.Setup(c => c.ExecuteQueryAsync(
                It.IsAny<GetSelectableContractTasksForConsultantInWeekQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(eligible);

        var leaveTypes = new Mock<ILeaveTypesAccessModule>();
        leaveTypes.Setup(l => l.LeaveTypeExistsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(leaveTypeExists);
        leaveTypes.Setup(l => l.GetUserLeaveAllowanceAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(leaveAllowanceDays);

        return new ApplyTimesheetWeekBookingsValidator(uow.Object, workdays.Object, contracts.Object, leaveTypes.Object);
    }

    private static ApplyTimesheetWeekBookingsCommand ValidCmd(
        TimeEntryInputDto[]? timeEntries = null,
        LeaveBookingInputDto[]? leaveBookings = null) =>
        new(UserId, Year, Week, timeEntries ?? [], leaveBookings ?? []);

    // --- Existing time-entry tests (use new DTO name) ---

    [Fact]
    public async Task Valid_Passes()
    {
        var result = await BuildValidator().ValidateAsync(ValidCmd(
            timeEntries: [new TimeEntryInputDto(TaskId, Mon, 8.00m)]));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task EmptyBookings_Passes()
    {
        var result = await BuildValidator().ValidateAsync(ValidCmd());
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task DateOutsideWeek_Fails()
    {
        var outsideDate = new DateOnly(2026, 5, 25); // Monday of next week
        var result = await BuildValidator().ValidateAsync(ValidCmd(
            timeEntries: [new TimeEntryInputDto(TaskId, outsideDate, 8.00m)]));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == TimesheetErrors.DateOutsideWeek.Code);
    }

    [Fact]
    public async Task DateNotBusinessDay_Fails()
    {
        var result = await BuildValidator(isBusinessDay: false).ValidateAsync(ValidCmd(
            timeEntries: [new TimeEntryInputDto(TaskId, Mon, 8.00m)]));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == TimesheetErrors.DateNotBusinessDay.Code);
    }

    [Fact]
    public async Task ContractTaskNotEligible_Fails()
    {
        var result = await BuildValidator(taskEligible: false).ValidateAsync(ValidCmd(
            timeEntries: [new TimeEntryInputDto(TaskId, Mon, 8.00m)]));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == TimesheetErrors.ContractTaskNotEligible.Code);
    }

    [Theory]
    [InlineData(0.00)]
    [InlineData(-0.25)]
    [InlineData(8.25)]
    [InlineData(0.10)]
    [InlineData(0.33)]
    public async Task InvalidDurationHours_Fails(double duration)
    {
        var result = await BuildValidator().ValidateAsync(ValidCmd(
            timeEntries: [new TimeEntryInputDto(TaskId, Mon, (decimal)duration)]));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == TimesheetErrors.InvalidDurationHours.Code);
    }

    [Theory]
    [InlineData(0.25)]
    [InlineData(0.50)]
    [InlineData(0.75)]
    [InlineData(4.00)]
    [InlineData(8.00)]
    public async Task ValidDurationHours_Passes(double duration)
    {
        var result = await BuildValidator().ValidateAsync(ValidCmd(
            timeEntries: [new TimeEntryInputDto(TaskId, Mon, (decimal)duration)]));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task NonDraftStatus_Fails()
    {
        var week = TimesheetWeek.Create(UserId, Year, Week);
        typeof(TimesheetWeek)
            .GetProperty("Status", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)!
            .SetValue(week, TimesheetStatus.Submitted);

        var result = await BuildValidator(existingWeek: week).ValidateAsync(ValidCmd(
            timeEntries: [new TimeEntryInputDto(TaskId, Mon, 8.00m)]));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == TimesheetErrors.NotDraft.Code);
    }

    // --- Leave booking tests ---

    [Fact]
    public async Task LeaveBooking_Valid_Passes()
    {
        var result = await BuildValidator(leaveTypeExists: true, leaveAllowanceDays: 20m)
            .ValidateAsync(ValidCmd(
                leaveBookings: [new LeaveBookingInputDto(LeaveTypeId, Mon, 8.00m)]));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task LeaveBooking_DateOutsideWeek_Fails()
    {
        var outsideDate = new DateOnly(2026, 5, 25);
        var result = await BuildValidator(leaveTypeExists: true, leaveAllowanceDays: 20m)
            .ValidateAsync(ValidCmd(
                leaveBookings: [new LeaveBookingInputDto(LeaveTypeId, outsideDate, 8.00m)]));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == TimesheetErrors.DateOutsideWeek.Code);
    }

    [Fact]
    public async Task LeaveBooking_DateNotBusinessDay_Fails()
    {
        var result = await BuildValidator(isBusinessDay: false, leaveTypeExists: true, leaveAllowanceDays: 20m)
            .ValidateAsync(ValidCmd(
                leaveBookings: [new LeaveBookingInputDto(LeaveTypeId, Mon, 8.00m)]));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == TimesheetErrors.DateNotBusinessDay.Code);
    }

    [Fact]
    public async Task LeaveBooking_UnknownLeaveType_Fails()
    {
        var result = await BuildValidator(leaveTypeExists: false)
            .ValidateAsync(ValidCmd(
                leaveBookings: [new LeaveBookingInputDto(Guid.NewGuid(), Mon, 8.00m)]));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == TimesheetErrors.LeaveTypeNotFound.Code);
    }

    [Fact]
    public async Task LeaveBooking_InvalidDuration_Fails()
    {
        var result = await BuildValidator(leaveTypeExists: true, leaveAllowanceDays: 20m)
            .ValidateAsync(ValidCmd(
                leaveBookings: [new LeaveBookingInputDto(LeaveTypeId, Mon, 0.1m)]));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == TimesheetErrors.InvalidDurationHours.Code);
    }

    [Fact]
    public async Task LeaveBooking_NullAllowance_Unlimited_Passes()
    {
        // 40 hours proposed — no allowance cap
        var result = await BuildValidator(leaveTypeExists: true, leaveAllowanceDays: null)
            .ValidateAsync(ValidCmd(
                leaveBookings: [new LeaveBookingInputDto(LeaveTypeId, Mon, 8.00m)]));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task LeaveBooking_ExactlyAtAllowance_Passes()
    {
        // Allowance = 1 day = 8h. Proposed = 8h. Should pass.
        var result = await BuildValidator(leaveTypeExists: true, leaveAllowanceDays: 1m)
            .ValidateAsync(ValidCmd(
                leaveBookings: [new LeaveBookingInputDto(LeaveTypeId, Mon, 8.00m)]));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task LeaveBooking_OverAllowanceByEpsilon_Fails()
    {
        // Allowance = 1 day = 8h. Proposed = 8.25h. Should fail.
        var result = await BuildValidator(leaveTypeExists: true, leaveAllowanceDays: 1m)
            .ValidateAsync(ValidCmd(
                leaveBookings: [new LeaveBookingInputDto(LeaveTypeId, Mon, 8.00m),
                                new LeaveBookingInputDto(LeaveTypeId, Tue, 0.25m)]));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == TimesheetErrors.LeaveAllowanceExceeded.Code);
    }

    [Fact]
    public async Task LeaveBooking_OtherWeekConsumption_CountedTowardAllowance()
    {
        // Other week already has 8h. Allowance = 1 day = 8h. Proposing 0.25h more → over.
        var otherWeek = TimesheetWeek.Create(UserId, Year, Week + 1);
        otherWeek.ApplyLeaveBookings([new LeaveBookingDto(LeaveTypeId, new DateOnly(2026, 5, 25), 8.00m)]);

        var result = await BuildValidator(
                leaveTypeExists: true,
                leaveAllowanceDays: 1m,
                otherWeeks: [otherWeek])
            .ValidateAsync(ValidCmd(
                leaveBookings: [new LeaveBookingInputDto(LeaveTypeId, Mon, 0.25m)]));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == TimesheetErrors.LeaveAllowanceExceeded.Code);
    }

    [Fact]
    public async Task LeaveBooking_UnderAllowanceWithOtherWeek_Passes()
    {
        // Other week has 4h. Allowance = 2 days = 16h. Proposing 8h → total 12h < 16h.
        var otherWeek = TimesheetWeek.Create(UserId, Year, Week + 1);
        otherWeek.ApplyLeaveBookings([new LeaveBookingDto(LeaveTypeId, new DateOnly(2026, 5, 25), 4.00m)]);

        var result = await BuildValidator(
                leaveTypeExists: true,
                leaveAllowanceDays: 2m,
                otherWeeks: [otherWeek])
            .ValidateAsync(ValidCmd(
                leaveBookings: [new LeaveBookingInputDto(LeaveTypeId, Mon, 8.00m)]));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task LeaveBooking_MixedLeaveTypes_IndependentAllowanceChecks()
    {
        // Type A: 1 day allowance, proposing 8h → exactly at limit
        // Type B: 1 day allowance, proposing 8h → exactly at limit
        var typeA = Guid.NewGuid();
        var typeB = Guid.NewGuid();

        var leaveTypes = new Mock<ILeaveTypesAccessModule>();
        leaveTypes.Setup(l => l.LeaveTypeExistsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        leaveTypes.Setup(l => l.GetUserLeaveAllowanceAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1m);

        var uow = new Mock<IUnitOfWork>();
        var repo = new Mock<IRepository<TimesheetWeek>>();
        repo.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<TimesheetWeek, bool>>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
            .ReturnsAsync((TimesheetWeek?)null);
        repo.Setup(r => r.GetAllAsListAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<TimesheetWeek, bool>>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
            .ReturnsAsync((IEnumerable<TimesheetWeek>)[]);
        uow.Setup(u => u.RepositoryFor<TimesheetWeek>()).Returns(repo.Object);

        var workdays = new Mock<IWorkdaysAccessModule>();
        workdays.Setup(w => w.IsBusinessDay(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var contracts = new Mock<IContractsAccessModule>();
        contracts.Setup(c => c.ExecuteQueryAsync(
                It.IsAny<GetSelectableContractTasksForConsultantInWeekQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<SelectableContractTaskDto>)[]);

        var validator = new ApplyTimesheetWeekBookingsValidator(uow.Object, workdays.Object, contracts.Object, leaveTypes.Object);

        var cmd = new ApplyTimesheetWeekBookingsCommand(
            UserId, Year, Week,
            TimeEntries: [],
            LeaveBookings: [
                new LeaveBookingInputDto(typeA, Mon, 8.00m),
                new LeaveBookingInputDto(typeB, Tue, 8.00m),
            ]);

        var result = await validator.ValidateAsync(cmd);
        result.IsValid.ShouldBeTrue();
    }
}
