using Moq;
using Shouldly;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Common.Pagination;
using Tsz.Modules.Timesheets.Domain.Timesheets;
using Tsz.Modules.Timesheets.Features;
using Tsz.Modules.Users.Contracts;
using Tsz.Modules.Users.Contracts.Queries;
using SortDirection = Tsz.Infrastructure.Common.Pagination.SortDirection;

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
    public async Task NoSubmittedWeeks_ReturnsEmptyPage()
    {
        var handler = BuildHandler();

        var result = await handler.HandleAsync(new GetPendingApprovalsQuery());

        result.Items.ShouldBeEmpty();
        result.Total.ShouldBe(0);
        result.NextCursor.ShouldBeNull();
    }

    [Fact]
    public async Task OnlySubmittedWeeksReturned_RepositoryReceivesSubmittedFilter()
    {
        var submitted = TimesheetWeek.Create(UserId1, 2026, 21);
        submitted.Submit();

        var names = new Dictionary<Guid, string> { [UserId1] = "Alice Smith" };
        var handler = BuildHandler(weeks: [submitted], nameById: names);

        var result = await handler.HandleAsync(new GetPendingApprovalsQuery());

        result.Items.Count.ShouldBe(1);
        result.Items[0].UserId.ShouldBe(UserId1);
        result.Items[0].IsoYear.ShouldBe(2026);
        result.Items[0].IsoWeek.ShouldBe(21);
        result.Total.ShouldBe(1);
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

        result.Items.Count.ShouldBe(1);
        result.Items[0].TotalHours.ShouldBe(8m);
    }

    [Fact]
    public async Task UserName_ResolvedFromCrossModuleQuery()
    {
        var week = TimesheetWeek.Create(UserId1, 2026, 21);
        week.Submit();

        var names = new Dictionary<Guid, string> { [UserId1] = "Bob Jones" };
        var handler = BuildHandler(weeks: [week], nameById: names);

        var result = await handler.HandleAsync(new GetPendingApprovalsQuery());

        result.Items[0].UserName.ShouldBe("Bob Jones");
    }

    [Fact]
    public async Task DefaultSort_OldestWeekFirst()
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

        result.Items.Count.ShouldBe(3);
        result.Items[0].IsoYear.ShouldBe(2025);
        result.Items[0].IsoWeek.ShouldBe(52);
        result.Items[1].IsoWeek.ShouldBe(20);
        result.Items[2].IsoWeek.ShouldBe(22);
    }

    [Fact]
    public async Task SortByEmployee_OrdersByUserName()
    {
        var weekA = TimesheetWeek.Create(UserId1, 2026, 21);
        weekA.Submit();
        var weekB = TimesheetWeek.Create(UserId2, 2026, 21);
        weekB.Submit();

        var names = new Dictionary<Guid, string>
        {
            [UserId1] = "Zach Adams",
            [UserId2] = "Alice Brown",
        };
        var handler = BuildHandler(weeks: [weekA, weekB], nameById: names);

        var result = await handler.HandleAsync(
            new GetPendingApprovalsQuery(null, "employee", SortDirection.Asc, 0, null, false, null, null));

        result.Items.Count.ShouldBe(2);
        result.Items[0].UserName.ShouldBe("Alice Brown");
        result.Items[1].UserName.ShouldBe("Zach Adams");
    }

    [Fact]
    public async Task SortByEmployee_Desc()
    {
        var weekA = TimesheetWeek.Create(UserId1, 2026, 21);
        weekA.Submit();
        var weekB = TimesheetWeek.Create(UserId2, 2026, 21);
        weekB.Submit();

        var names = new Dictionary<Guid, string>
        {
            [UserId1] = "Alice Brown",
            [UserId2] = "Zach Adams",
        };
        var handler = BuildHandler(weeks: [weekA, weekB], nameById: names);

        var result = await handler.HandleAsync(
            new GetPendingApprovalsQuery(null, "employee", SortDirection.Desc, 0, null, false, null, null));

        result.Items[0].UserName.ShouldBe("Zach Adams");
        result.Items[1].UserName.ShouldBe("Alice Brown");
    }

    [Fact]
    public async Task SortByTotalHours_OrdersByHours()
    {
        var heavy = TimesheetWeek.Create(UserId1, 2026, 21);
        heavy.ApplyBookings([new TimeEntryBookingDto(TaskId, new DateOnly(2026, 5, 18), 8m)], []);
        heavy.Submit();

        var light = TimesheetWeek.Create(UserId2, 2026, 21);
        light.ApplyBookings([new TimeEntryBookingDto(TaskId, new DateOnly(2026, 5, 18), 4m)], []);
        light.Submit();

        var names = new Dictionary<Guid, string>
        {
            [UserId1] = "Alice",
            [UserId2] = "Bob",
        };
        var handler = BuildHandler(weeks: [heavy, light], nameById: names);

        var result = await handler.HandleAsync(
            new GetPendingApprovalsQuery(null, "totalHours", SortDirection.Asc, 0, null, false, null, null));

        result.Items[0].TotalHours.ShouldBe(4m);
        result.Items[1].TotalHours.ShouldBe(8m);
    }

    [Fact]
    public async Task Search_FiltersByUserNameCaseInsensitive()
    {
        var weekA = TimesheetWeek.Create(UserId1, 2026, 21);
        weekA.Submit();
        var weekB = TimesheetWeek.Create(UserId2, 2026, 21);
        weekB.Submit();

        var names = new Dictionary<Guid, string>
        {
            [UserId1] = "Alice Smith",
            [UserId2] = "Bob Jones",
        };
        var handler = BuildHandler(weeks: [weekA, weekB], nameById: names);

        var result = await handler.HandleAsync(
            new GetPendingApprovalsQuery("alice", null, SortDirection.Asc, 0, null, false, null, null));

        result.Items.Count.ShouldBe(1);
        result.Items[0].UserName.ShouldBe("Alice Smith");
        result.Total.ShouldBe(1);
    }

    [Fact]
    public async Task Search_MatchesRawWeekNumber()
    {
        var week21 = TimesheetWeek.Create(UserId1, 2026, 21);
        week21.Submit();
        var week22 = TimesheetWeek.Create(UserId2, 2026, 22);
        week22.Submit();

        var names = new Dictionary<Guid, string>
        {
            [UserId1] = "Alice Smith",
            [UserId2] = "Bob Jones",
        };
        var handler = BuildHandler(weeks: [week21, week22], nameById: names);

        var result = await handler.HandleAsync(
            new GetPendingApprovalsQuery("21", null, SortDirection.Asc, 0, null, false, null, null));

        result.Items.Count.ShouldBe(1);
        result.Items[0].IsoWeek.ShouldBe(21);
    }

    [Fact]
    public async Task Search_MatchesFormattedWeekLabel()
    {
        var week21_2026 = TimesheetWeek.Create(UserId1, 2026, 21);
        week21_2026.Submit();
        var week21_2025 = TimesheetWeek.Create(UserId2, 2025, 21);
        week21_2025.Submit();

        var names = new Dictionary<Guid, string>
        {
            [UserId1] = "Alice Smith",
            [UserId2] = "Bob Jones",
        };
        var handler = BuildHandler(weeks: [week21_2026, week21_2025], nameById: names);

        var result = await handler.HandleAsync(
            new GetPendingApprovalsQuery("2026-W21", null, SortDirection.Asc, 0, null, false, null, null));

        result.Items.Count.ShouldBe(1);
        result.Items[0].IsoYear.ShouldBe(2026);
        result.Items[0].IsoWeek.ShouldBe(21);
    }

    [Fact]
    public async Task Search_MatchesYearOnly()
    {
        var week21_2026 = TimesheetWeek.Create(UserId1, 2026, 21);
        week21_2026.Submit();
        var week21_2025 = TimesheetWeek.Create(UserId2, 2025, 21);
        week21_2025.Submit();

        var names = new Dictionary<Guid, string>
        {
            [UserId1] = "Alice Smith",
            [UserId2] = "Bob Jones",
        };
        var handler = BuildHandler(weeks: [week21_2026, week21_2025], nameById: names);

        var result = await handler.HandleAsync(
            new GetPendingApprovalsQuery("2025", null, SortDirection.Asc, 0, null, false, null, null));

        result.Items.Count.ShouldBe(1);
        result.Items[0].IsoYear.ShouldBe(2025);
    }

    [Fact]
    public async Task DateRange_KeepsWeeksOverlappingFromTo()
    {
        // ISO 2026-W20 is Mon 2026-05-11 → Sun 2026-05-17.
        // ISO 2026-W21 is Mon 2026-05-18 → Sun 2026-05-24.
        // ISO 2026-W22 is Mon 2026-05-25 → Sun 2026-05-31.
        var week20 = TimesheetWeek.Create(UserId1, 2026, 20);
        week20.Submit();
        var week21 = TimesheetWeek.Create(UserId1, 2026, 21);
        week21.Submit();
        var week22 = TimesheetWeek.Create(UserId2, 2026, 22);
        week22.Submit();

        var names = new Dictionary<Guid, string>
        {
            [UserId1] = "Alice Smith",
            [UserId2] = "Bob Jones",
        };
        var handler = BuildHandler(weeks: [week20, week21, week22], nameById: names);

        // Range 2026-05-18 → 2026-05-24 fully matches W21 only.
        var result = await handler.HandleAsync(
            new GetPendingApprovalsQuery(null, null, SortDirection.Asc, 0, null, false,
                new DateOnly(2026, 5, 18), new DateOnly(2026, 5, 24)));

        result.Items.Count.ShouldBe(1);
        result.Items[0].IsoWeek.ShouldBe(21);
    }

    [Fact]
    public async Task DateRange_FromOnly_KeepsWeeksEndingOnOrAfter()
    {
        var week20 = TimesheetWeek.Create(UserId1, 2026, 20);
        week20.Submit();
        var week22 = TimesheetWeek.Create(UserId2, 2026, 22);
        week22.Submit();

        var names = new Dictionary<Guid, string>
        {
            [UserId1] = "Alice Smith",
            [UserId2] = "Bob Jones",
        };
        var handler = BuildHandler(weeks: [week20, week22], nameById: names);

        // From=2026-05-20: W20 ends 2026-05-17 → excluded; W22 ends 2026-05-31 → included.
        var result = await handler.HandleAsync(
            new GetPendingApprovalsQuery(null, null, SortDirection.Asc, 0, null, false,
                new DateOnly(2026, 5, 20), null));

        result.Items.Count.ShouldBe(1);
        result.Items[0].IsoWeek.ShouldBe(22);
    }

    [Fact]
    public async Task DateRange_ToOnly_KeepsWeeksStartingOnOrBefore()
    {
        var week20 = TimesheetWeek.Create(UserId1, 2026, 20);
        week20.Submit();
        var week22 = TimesheetWeek.Create(UserId2, 2026, 22);
        week22.Submit();

        var names = new Dictionary<Guid, string>
        {
            [UserId1] = "Alice Smith",
            [UserId2] = "Bob Jones",
        };
        var handler = BuildHandler(weeks: [week20, week22], nameById: names);

        // To=2026-05-20: W20 starts 2026-05-11 → included; W22 starts 2026-05-25 → excluded.
        var result = await handler.HandleAsync(
            new GetPendingApprovalsQuery(null, null, SortDirection.Asc, 0, null, false,
                null, new DateOnly(2026, 5, 20)));

        result.Items.Count.ShouldBe(1);
        result.Items[0].IsoWeek.ShouldBe(20);
    }

    [Fact]
    public async Task DateRange_NoOverlap_ReturnsEmpty()
    {
        var week21 = TimesheetWeek.Create(UserId1, 2026, 21);
        week21.Submit();

        var names = new Dictionary<Guid, string> { [UserId1] = "Alice Smith" };
        var handler = BuildHandler(weeks: [week21], nameById: names);

        var result = await handler.HandleAsync(
            new GetPendingApprovalsQuery(null, null, SortDirection.Asc, 0, null, false,
                new DateOnly(2030, 1, 1), new DateOnly(2030, 12, 31)));

        result.Items.ShouldBeEmpty();
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

        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(r => r.UserId == UserId1 && r.UserName == "Alice Smith");
        result.Items.ShouldContain(r => r.UserId == UserId2 && r.UserName == "Bob Jones");
    }
}
