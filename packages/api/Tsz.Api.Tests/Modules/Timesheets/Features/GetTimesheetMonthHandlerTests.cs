using Moq;
using Shouldly;
using Tsz.Modules.Contracts.Contracts;
using Tsz.Modules.Contracts.Contracts.Queries;
using Tsz.Modules.LeaveTypes.Contracts;
using Tsz.Modules.LeaveTypes.Contracts.Queries;
using Tsz.Modules.Timesheets.Domain.Holidays;
using Tsz.Modules.Timesheets.Domain.Timesheets;
using Tsz.Modules.Timesheets.Features;
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Tests.Modules.Timesheets.Features;

public class GetTimesheetMonthHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid TaskId = Guid.NewGuid();
    private static readonly Guid LeaveTypeId = Guid.NewGuid();

    private static GetTimesheetMonthHandler BuildHandler(
        IEnumerable<TimesheetWeek>? weeks = null,
        bool isBusinessDay = true,
        IReadOnlyList<ContractTaskDisplayInfoDto>? displayInfo = null,
        IReadOnlyList<ActiveLeaveTypeDto>? activeLeaveTypes = null)
    {
        var repo = new Mock<IRepository<TimesheetWeek>>();
        repo.Setup(r => r.GetAllAsListAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<TimesheetWeek, bool>>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
            .ReturnsAsync(weeks ?? []);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<TimesheetWeek>()).Returns(repo.Object);

        var businessDayService = new Mock<IBusinessDayService>();
        businessDayService.Setup(w => w.GetDayKindsAsync(
                It.IsAny<IReadOnlyCollection<DateOnly>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<DateOnly> dates, CancellationToken _) =>
                (IReadOnlyList<DayKindInfo>)dates.Select(d =>
                {
                    var isWeekend = d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
                    var biz = !isWeekend && isBusinessDay;
                    return new DayKindInfo(d, biz, null);
                }).ToList());

        var contracts = new Mock<IContractsAccessModule>();
        contracts.Setup(c => c.ExecuteQueryAsync(
                It.IsAny<GetContractTaskDisplayInfoByIdsQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(displayInfo ?? []);

        var leaveTypesModule = new Mock<ILeaveTypesAccessModule>();
        leaveTypesModule.Setup(l => l.GetActiveLeaveTypesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeLeaveTypes ?? []);

        return new GetTimesheetMonthHandler(uow.Object, contracts.Object, businessDayService.Object, leaveTypesModule.Object);
    }

    [Fact]
    public async Task NoWeeks_ReturnsEmptyMonthWithZeroTotal()
    {
        var handler = BuildHandler();
        var result = await handler.HandleAsync(new GetTimesheetMonthQuery(UserId, 2026, 5));

        result.UserId.ShouldBe(UserId);
        result.Year.ShouldBe(2026);
        result.Month.ShouldBe(5);
        result.Weeks.ShouldBeEmpty();
        result.MonthTotalHours.ShouldBe(0m);
    }

    [Fact]
    public async Task WeekWithTimeEntry_ComputesDayAndWeekTotals()
    {
        // Week 21 of 2026: Mon 18 May - Sun 24 May — Monday is inside May.
        var week = TimesheetWeek.Create(UserId, 2026, 21);
        week.ApplyBookings([new TimeEntryBookingDto(TaskId, new DateOnly(2026, 5, 18), 8m)], []);

        var displayInfo = new List<ContractTaskDisplayInfoDto>
        {
            new(TaskId, "Dev", Guid.NewGuid(), "Contract A", Guid.NewGuid(), "Customer X")
        };

        var handler = BuildHandler(weeks: [week], displayInfo: displayInfo);
        var result = await handler.HandleAsync(new GetTimesheetMonthQuery(UserId, 2026, 5));

        result.Weeks.Count.ShouldBe(1);
        var weekDto = result.Weeks[0];
        weekDto.IsoWeek.ShouldBe(21);
        weekDto.Status.ShouldBe("Draft");
        weekDto.Days.Count.ShouldBe(7);

        var monday = weekDto.Days.First(d => d.Date == new DateOnly(2026, 5, 18));
        monday.TotalHours.ShouldBe(8m);
        monday.TimeEntries.Count.ShouldBe(1);
        monday.TimeEntries[0].DurationHours.ShouldBe(8m);
        monday.TimeEntries[0].TaskName.ShouldBe("Dev");
        monday.TimeEntries[0].CustomerName.ShouldBe("Customer X");

        result.MonthTotalHours.ShouldBe(8m);
    }

    [Fact]
    public async Task WeekWithLeaveBooking_ComputesLeaveTotal()
    {
        var week = TimesheetWeek.Create(UserId, 2026, 21);
        week.ApplyBookings([], [new LeaveBookingDto(LeaveTypeId, new DateOnly(2026, 5, 18), 4m)]);

        var activeLeaveTypes = new List<ActiveLeaveTypeDto> { new(LeaveTypeId, "Verlof", LeaveAllowed.Limited, 20m) };
        var handler = BuildHandler(weeks: [week], activeLeaveTypes: activeLeaveTypes);
        var result = await handler.HandleAsync(new GetTimesheetMonthQuery(UserId, 2026, 5));

        result.MonthTotalHours.ShouldBe(4m);
        var monday = result.Weeks[0].Days.First(d => d.Date == new DateOnly(2026, 5, 18));
        monday.TotalHours.ShouldBe(4m);
        monday.LeaveBookings.Count.ShouldBe(1);
        monday.LeaveBookings[0].LeaveTypeName.ShouldBe("Verlof");
    }

    [Fact]
    public async Task PerTaskSummary_GroupsByTaskAcrossWeek()
    {
        var week = TimesheetWeek.Create(UserId, 2026, 21);
        week.ApplyBookings([
            new TimeEntryBookingDto(TaskId, new DateOnly(2026, 5, 18), 8m),
            new TimeEntryBookingDto(TaskId, new DateOnly(2026, 5, 19), 4m),
        ], []);

        var displayInfo = new List<ContractTaskDisplayInfoDto>
        {
            new(TaskId, "Dev", Guid.NewGuid(), "Contract A", Guid.NewGuid(), "Customer X")
        };

        var handler = BuildHandler(weeks: [week], displayInfo: displayInfo);
        var result = await handler.HandleAsync(new GetTimesheetMonthQuery(UserId, 2026, 5));

        var summary = result.Weeks[0].PerTaskSummary;
        summary.Count.ShouldBe(1);
        summary[0].TaskName.ShouldBe("Dev");
        summary[0].TotalHours.ShouldBe(12m);
    }

    [Fact]
    public async Task PerLeaveTypeSummary_GroupsByLeaveType()
    {
        var week = TimesheetWeek.Create(UserId, 2026, 21);
        week.ApplyBookings([], [
            new LeaveBookingDto(LeaveTypeId, new DateOnly(2026, 5, 18), 8m),
            new LeaveBookingDto(LeaveTypeId, new DateOnly(2026, 5, 19), 8m),
        ]);

        var activeLeaveTypes = new List<ActiveLeaveTypeDto> { new(LeaveTypeId, "Verlof", LeaveAllowed.Limited, 20m) };
        var handler = BuildHandler(weeks: [week], activeLeaveTypes: activeLeaveTypes);
        var result = await handler.HandleAsync(new GetTimesheetMonthQuery(UserId, 2026, 5));

        var summary = result.Weeks[0].PerLeaveTypeSummary;
        summary.Count.ShouldBe(1);
        summary[0].LeaveTypeName.ShouldBe("Verlof");
        summary[0].TotalHours.ShouldBe(16m);
    }

    [Fact]
    public async Task MonthTotalHours_SumsAcrossAllWeeks()
    {
        var week1 = TimesheetWeek.Create(UserId, 2026, 18); // Mon Apr 27 — inside April? Actually week 18 of 2026: Mon Apr 27 — not in May
        var week21 = TimesheetWeek.Create(UserId, 2026, 21);
        week21.ApplyBookings([new TimeEntryBookingDto(TaskId, new DateOnly(2026, 5, 18), 8m)], []);
        var week22 = TimesheetWeek.Create(UserId, 2026, 22);
        week22.ApplyBookings([new TimeEntryBookingDto(TaskId, new DateOnly(2026, 5, 25), 6m)], []);

        var displayInfo = new List<ContractTaskDisplayInfoDto>
        {
            new(TaskId, "Dev", Guid.NewGuid(), "Contract A", Guid.NewGuid(), "Customer X")
        };

        // week1 has Monday Apr 27 which is NOT in May, so should be excluded
        var handler = BuildHandler(weeks: [week1, week21, week22], displayInfo: displayInfo);
        var result = await handler.HandleAsync(new GetTimesheetMonthQuery(UserId, 2026, 5));

        // Only weeks 21 and 22 have Monday in May
        result.Weeks.Count.ShouldBe(2);
        result.MonthTotalHours.ShouldBe(14m);
    }

    [Fact]
    public async Task StatusIsReflected_InWeekDto()
    {
        var week = TimesheetWeek.Create(UserId, 2026, 21);
        week.Submit();

        var handler = BuildHandler(weeks: [week]);
        var result = await handler.HandleAsync(new GetTimesheetMonthQuery(UserId, 2026, 5));

        result.Weeks[0].Status.ShouldBe("Submitted");
    }
}
