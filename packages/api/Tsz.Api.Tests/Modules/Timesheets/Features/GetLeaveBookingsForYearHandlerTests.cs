using Moq;
using Shouldly;
using Tsz.Infrastructure.Abstractions;
using Tsz.Modules.LeaveTypes.Contracts;
using Tsz.Modules.LeaveTypes.Contracts.Queries;
using Tsz.Modules.Timesheets.Domain.Timesheets;
using Tsz.Modules.Timesheets.Features;

namespace Tsz.Api.Tests.Modules.Timesheets.Features;

public class GetLeaveBookingsForYearHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid LeaveTypeId = Guid.NewGuid();

    private static GetLeaveBookingsForYearHandler BuildHandler(
        IEnumerable<TimesheetWeek>? weeks = null,
        IReadOnlyList<ActiveLeaveTypeDto>? activeLeaveTypes = null)
    {
        var repo = new Mock<IRepository<TimesheetWeek>>();
        repo.Setup(r => r.GetAllAsListAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<TimesheetWeek, bool>>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
            .ReturnsAsync(weeks?.ToList() ?? []);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<TimesheetWeek>()).Returns(repo.Object);

        var leaveTypesModule = new Mock<ILeaveTypesAccessModule>();
        leaveTypesModule.Setup(l => l.GetActiveLeaveTypesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeLeaveTypes ?? [new ActiveLeaveTypeDto(LeaveTypeId, "Verlof", LeaveAllowed.Limited, 20m)]);

        return new GetLeaveBookingsForYearHandler(uow.Object, leaveTypesModule.Object);
    }

    [Fact]
    public async Task NoWeeks_ReturnsEmpty()
    {
        var handler = BuildHandler();
        var result = await handler.HandleAsync(new GetLeaveBookingsForYearQuery(UserId, 2026));
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task WeekWithLeaveBookings_ProjectsLeaveTypeName()
    {
        var week = TimesheetWeek.Create(UserId, 2026, 21);
        week.ApplyBookings([], [new LeaveBookingDto(LeaveTypeId, new DateOnly(2026, 5, 18), 8m)]);

        var handler = BuildHandler(
            weeks: [week],
            activeLeaveTypes: [new ActiveLeaveTypeDto(LeaveTypeId, "Verlof", LeaveAllowed.Limited, 20m)]);

        var result = await handler.HandleAsync(new GetLeaveBookingsForYearQuery(UserId, 2026));

        result.Count.ShouldBe(1);
        result[0].LeaveTypeId.ShouldBe(LeaveTypeId);
        result[0].LeaveTypeName.ShouldBe("Verlof");
        result[0].Date.ShouldBe(new DateOnly(2026, 5, 18));
        result[0].DurationHours.ShouldBe(8m);
    }

    [Fact]
    public async Task YearFilter_ExcludesBookingsFromOtherCalendarYear()
    {
        // Bookings are filtered by `booking.Date.Year`, NOT by the parent
        // TimesheetWeek's IsoYear. A booking dated 2027 belongs to year 2027 even
        // if its containing week has IsoYear 2026 (or vice-versa around year
        // boundaries). This guards against accidentally filtering by week year.
        var week = TimesheetWeek.Create(UserId, 2026, 21);
        week.ApplyBookings([], [
            new LeaveBookingDto(LeaveTypeId, new DateOnly(2026, 5, 18), 8m),
        ]);

        var weekNextYear = TimesheetWeek.Create(UserId, 2027, 5);
        weekNextYear.ApplyBookings([], [
            new LeaveBookingDto(LeaveTypeId, new DateOnly(2027, 2, 1), 4m),
        ]);

        var handler = BuildHandler(weeks: [week, weekNextYear]);

        var result = await handler.HandleAsync(new GetLeaveBookingsForYearQuery(UserId, 2026));

        result.Count.ShouldBe(1);
        result[0].Date.Year.ShouldBe(2026);
    }

    [Fact]
    public async Task SortOrder_ByDateThenLeaveTypeId()
    {
        var leaveTypeA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
        var leaveTypeB = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
        var week = TimesheetWeek.Create(UserId, 2026, 21);
        week.ApplyBookings([], [
            new LeaveBookingDto(leaveTypeB, new DateOnly(2026, 5, 19), 4m),
            new LeaveBookingDto(leaveTypeA, new DateOnly(2026, 5, 19), 4m),
            new LeaveBookingDto(leaveTypeA, new DateOnly(2026, 5, 18), 8m),
        ]);

        var handler = BuildHandler(
            weeks: [week],
            activeLeaveTypes: [
                new ActiveLeaveTypeDto(leaveTypeA, "A", LeaveAllowed.Limited, 20m),
                new ActiveLeaveTypeDto(leaveTypeB, "B", LeaveAllowed.Limited, 20m),
            ]);

        var result = await handler.HandleAsync(new GetLeaveBookingsForYearQuery(UserId, 2026));

        result.Count.ShouldBe(3);
        result[0].Date.ShouldBe(new DateOnly(2026, 5, 18));
        result[1].Date.ShouldBe(new DateOnly(2026, 5, 19));
        result[1].LeaveTypeId.ShouldBe(leaveTypeA);
        result[2].Date.ShouldBe(new DateOnly(2026, 5, 19));
        result[2].LeaveTypeId.ShouldBe(leaveTypeB);
    }

    [Fact]
    public async Task MultipleWeeks_AggregatesAllBookings()
    {
        var week21 = TimesheetWeek.Create(UserId, 2026, 21);
        week21.ApplyBookings([], [new LeaveBookingDto(LeaveTypeId, new DateOnly(2026, 5, 18), 8m)]);

        var week22 = TimesheetWeek.Create(UserId, 2026, 22);
        week22.ApplyBookings([], [new LeaveBookingDto(LeaveTypeId, new DateOnly(2026, 5, 25), 4m)]);

        var handler = BuildHandler(weeks: [week21, week22]);

        var result = await handler.HandleAsync(new GetLeaveBookingsForYearQuery(UserId, 2026));

        result.Count.ShouldBe(2);
    }
}
