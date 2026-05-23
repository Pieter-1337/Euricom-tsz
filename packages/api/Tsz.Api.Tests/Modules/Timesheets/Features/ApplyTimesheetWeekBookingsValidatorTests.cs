using Moq;
using Shouldly;
using Tsz.Modules.Contracts.Contracts;
using Tsz.Modules.Contracts.Contracts.Queries;
using Tsz.Modules.Timesheets.Domain.Timesheets;
using Tsz.Modules.Timesheets.Features;
using Tsz.Modules.Workdays.Contracts;
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Tests.Modules.Timesheets.Features;

public class ApplyTimesheetWeekBookingsValidatorTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid TaskId = Guid.NewGuid();
    private const int Year = 2026;
    private const int Week = 21;

    // Mon 18 May 2026 .. Sun 24 May 2026
    private static readonly DateOnly Mon = new(2026, 5, 18);
    private static readonly DateOnly Tue = new(2026, 5, 19);
    private static readonly DateOnly Sat = new(2026, 5, 23);

    private static ApplyTimesheetWeekBookingsValidator BuildValidator(
        TimesheetWeek? existingWeek = null,
        bool isBusinessDay = true,
        bool taskEligible = true)
    {
        var uow = new Mock<IUnitOfWork>();
        var repo = new Mock<IRepository<TimesheetWeek>>();
        repo.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<TimesheetWeek, bool>>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
            .ReturnsAsync(existingWeek);
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

        return new ApplyTimesheetWeekBookingsValidator(uow.Object, workdays.Object, contracts.Object);
    }

    private static ApplyTimesheetWeekBookingsCommand ValidCmd(params BookingInputDto[] bookings) =>
        new(UserId, Year, Week, bookings);

    [Fact]
    public async Task Valid_Passes()
    {
        var result = await BuildValidator().ValidateAsync(ValidCmd(
            new BookingInputDto(TaskId, Mon, 8.00m)));
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
            new BookingInputDto(TaskId, outsideDate, 8.00m)));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == TimesheetErrors.DateOutsideWeek.Code);
    }

    [Fact]
    public async Task DateNotBusinessDay_Fails()
    {
        var result = await BuildValidator(isBusinessDay: false).ValidateAsync(ValidCmd(
            new BookingInputDto(TaskId, Mon, 8.00m)));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == TimesheetErrors.DateNotBusinessDay.Code);
    }

    [Fact]
    public async Task ContractTaskNotEligible_Fails()
    {
        var result = await BuildValidator(taskEligible: false).ValidateAsync(ValidCmd(
            new BookingInputDto(TaskId, Mon, 8.00m)));
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
            new BookingInputDto(TaskId, Mon, (decimal)duration)));
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
            new BookingInputDto(TaskId, Mon, (decimal)duration)));
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
            new BookingInputDto(TaskId, Mon, 8.00m)));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == TimesheetErrors.NotDraft.Code);
    }
}
