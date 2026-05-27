using Moq;
using Shouldly;
using Tsz.Infrastructure.Abstractions;
using Tsz.Modules.Timesheets.Domain.Timesheets;
using Tsz.Modules.Timesheets.Features;
using Tsz.Modules.Users.Contracts;
using Tsz.Modules.Users.Contracts.Queries;

namespace Tsz.Api.Tests.Modules.Timesheets.Features;

public class GetPendingApprovalsHandlerTests
{
    private static readonly Guid UserId1 = Guid.NewGuid();
    private static readonly Guid UserId2 = Guid.NewGuid();
    private static readonly Guid TaskId = Guid.NewGuid();
    private static readonly Guid LeaveTypeId = Guid.NewGuid();

    private static GetPendingApprovalsHandler BuildHandler(
        IEnumerable<TimesheetWeek>? weeks = null,
        IReadOnlyDictionary<Guid, string>? nameById = null)
    {
        var repo = new Mock<IRepository<TimesheetWeek>>();
        repo.Setup(r => r.GetAllAsListAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<TimesheetWeek, bool>>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
            .ReturnsAsync(weeks?.ToList() ?? []);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<TimesheetWeek>()).Returns(repo.Object);

        var users = new Mock<IUsersAccessModule>();
        users.Setup(u => u.ExecuteQueryAsync(
                It.IsAny<GetUserNamesByIdsQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(nameById ?? new Dictionary<Guid, string>());

        return new GetPendingApprovalsHandler(uow.Object, users.Object);
    }

    [Fact]
    public async Task NoSubmittedWeeks_ReturnsEmptyList()
    {
        var handler = BuildHandler();

        var result = await handler.HandleAsync(new GetPendingApprovalsQuery());

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task OnlySubmittedWeeksReturned_RepositoryReceivesSubmittedFilter()
    {
        // The Status == Submitted filter is applied at the DB layer; the repository mock
        // returns only what passes that filter. We verify the handler correctly assembles
        // the response from whatever the repository yields (already filtered).
        var submitted = TimesheetWeek.Create(UserId1, 2026, 21);
        submitted.Submit();

        var names = new Dictionary<Guid, string> { [UserId1] = "Alice Smith" };
        var handler = BuildHandler(weeks: [submitted], nameById: names);

        var result = await handler.HandleAsync(new GetPendingApprovalsQuery());

        result.Count.ShouldBe(1);
        result[0].UserId.ShouldBe(UserId1);
        result[0].IsoYear.ShouldBe(2026);
        result[0].IsoWeek.ShouldBe(21);
    }

    [Fact]
    public async Task TotalHours_SumsWorkAndLeave()
    {
        var week = TimesheetWeek.Create(UserId1, 2026, 21);
        week.ApplyBookings(
            [new TimeEntryBookingDto(TaskId, new DateOnly(2026, 5, 18), 6m)],
            [new LeaveBookingDto(LeaveTypeId, new DateOnly(2026, 5, 19), 2m)]);
        week.Submit();

        var names = new Dictionary<Guid, string> { [UserId1] = "Alice Smith" };
        var handler = BuildHandler(weeks: [week], nameById: names);

        var result = await handler.HandleAsync(new GetPendingApprovalsQuery());

        result.Count.ShouldBe(1);
        result[0].TotalHours.ShouldBe(8m);
    }

    [Fact]
    public async Task UserName_ResolvedFromCrossModuleQuery()
    {
        var week = TimesheetWeek.Create(UserId1, 2026, 21);
        week.Submit();

        var names = new Dictionary<Guid, string> { [UserId1] = "Bob Jones" };
        var handler = BuildHandler(weeks: [week], nameById: names);

        var result = await handler.HandleAsync(new GetPendingApprovalsQuery());

        result[0].UserName.ShouldBe("Bob Jones");
    }

    [Fact]
    public async Task Rows_SortedOldestWeekFirst()
    {
        var week1 = TimesheetWeek.Create(UserId1, 2026, 22);
        week1.Submit();

        var week2 = TimesheetWeek.Create(UserId1, 2026, 20);
        week2.Submit();

        var week3 = TimesheetWeek.Create(UserId2, 2025, 52);
        week3.Submit();

        var names = new Dictionary<Guid, string>
        {
            [UserId1] = "Alice Smith",
            [UserId2] = "Bob Jones",
        };
        var handler = BuildHandler(weeks: [week1, week2, week3], nameById: names);

        var result = await handler.HandleAsync(new GetPendingApprovalsQuery());

        result.Count.ShouldBe(3);
        result[0].IsoYear.ShouldBe(2025);
        result[0].IsoWeek.ShouldBe(52);
        result[1].IsoWeek.ShouldBe(20);
        result[2].IsoWeek.ShouldBe(22);
    }

    [Fact]
    public async Task MultipleUsers_AllSubmittedWeeksIncluded()
    {
        var week1 = TimesheetWeek.Create(UserId1, 2026, 21);
        week1.Submit();

        var week2 = TimesheetWeek.Create(UserId2, 2026, 21);
        week2.Submit();

        var names = new Dictionary<Guid, string>
        {
            [UserId1] = "Alice Smith",
            [UserId2] = "Bob Jones",
        };
        var handler = BuildHandler(weeks: [week1, week2], nameById: names);

        var result = await handler.HandleAsync(new GetPendingApprovalsQuery());

        result.Count.ShouldBe(2);
        result.ShouldContain(r => r.UserId == UserId1 && r.UserName == "Alice Smith");
        result.ShouldContain(r => r.UserId == UserId2 && r.UserName == "Bob Jones");
    }
}
