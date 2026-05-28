using System.Net;
using System.Net.Http.Json;
using Tsz.Api.Tests.Integration.TestAuth;
using Tsz.Infrastructure.Common.Pagination;
using Tsz.Modules.Timesheets.Domain.Timesheets;
using Tsz.Modules.Timesheets.Features;
using Tsz.Modules.Users.Contracts;
using Tsz.Modules.Users.Domain.Users;

namespace Tsz.Api.Tests.Integration;

public class PendingApprovalsEndpointsTests : IntegrationTestBase, IAsyncLifetime
{
    private const string CallerOid = TestAuthHandler.DefaultOid;

    public PendingApprovalsEndpointsTests(TestWebApplicationFactory factory) : base(factory) { }

    public async Task InitializeAsync()
    {
        await WithUowAsync(uow => uow.RepositoryFor<TimesheetWeek>().BatchHardDeleteAsync(_ => true));
        await WithUowAsync(uow => uow.RepositoryFor<User>().BatchHardDeleteAsync(_ => true));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<Guid> SeedCallerAsAsync(UserRole role, string firstName = "Caller", string lastName = "User")
    {
        var id = Guid.NewGuid();
        await WithUowAsync(async uow =>
        {
            var user = User.Create(firstName, lastName, $"caller-{id}@test.com", [role]);
            user.Id = id;
            user.LinkEntraOid(CallerOid);
            uow.RepositoryFor<User>().Add(user);
            await uow.SaveChangesAsync();
        });
        return id;
    }

    private async Task<Guid> SeedUserAsync(string firstName, string lastName, UserRole role = UserRole.User)
    {
        var id = Guid.NewGuid();
        await WithUowAsync(async uow =>
        {
            var user = User.Create(firstName, lastName, $"user-{id}@test.com", [role]);
            user.Id = id;
            uow.RepositoryFor<User>().Add(user);
            await uow.SaveChangesAsync();
        });
        return id;
    }

    private async Task SeedWeekWithStatusAsync(Guid userId, int isoYear, int isoWeek, TimesheetStatus status)
    {
        await WithUowAsync(async uow =>
        {
            var week = TimesheetWeek.Create(userId, isoYear, isoWeek);
            uow.RepositoryFor<TimesheetWeek>().Add(week);
            await uow.SaveChangesAsync();
        });

        if (status == TimesheetStatus.Submitted || status == TimesheetStatus.Approved)
        {
            await WithUowAsync(async uow =>
            {
                var week = await uow.RepositoryFor<TimesheetWeek>()
                    .FirstOrDefaultAsync(w => w.UserId == userId && w.IsoYear == isoYear && w.IsoWeek == isoWeek);
                week!.Submit();
                await uow.SaveChangesAsync();
            });
        }

        if (status == TimesheetStatus.Approved)
        {
            await WithUowAsync(async uow =>
            {
                var week = await uow.RepositoryFor<TimesheetWeek>()
                    .FirstOrDefaultAsync(w => w.UserId == userId && w.IsoYear == isoYear && w.IsoWeek == isoWeek);
                week!.Approve();
                await uow.SaveChangesAsync();
            });
        }
    }

    [Fact]
    public async Task GetPendingApprovals_AsAdmin_Returns200()
    {
        await SeedCallerAsAsync(UserRole.Admin);

        var response = await Client.GetAsync("/api/timesheet-weeks/pending-approvals");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetPendingApprovals_AsNonAdmin_Returns403()
    {
        await SeedCallerAsAsync(UserRole.User);

        var response = await Client.GetAsync("/api/timesheet-weeks/pending-approvals");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetPendingApprovals_NoSubmittedWeeks_ReturnsEmptyPage()
    {
        await SeedCallerAsAsync(UserRole.Admin);

        var response = await Client.GetAsync("/api/timesheet-weeks/pending-approvals");

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<KeysetPage<PendingApprovalDto>>(Json);
        Assert.NotNull(result);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.Total);
    }

    [Fact]
    public async Task GetPendingApprovals_WithSubmittedWeeks_ReturnsExpectedRows()
    {
        await SeedCallerAsAsync(UserRole.Admin);
        var consultantId = await SeedUserAsync("Jane", "Doe");
        await SeedWeekWithStatusAsync(consultantId, 2026, 21, TimesheetStatus.Submitted);

        var response = await Client.GetAsync("/api/timesheet-weeks/pending-approvals");

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<KeysetPage<PendingApprovalDto>>(Json);
        Assert.NotNull(result);
        Assert.Single(result.Items);
        Assert.Equal(consultantId, result.Items[0].UserId);
        Assert.Equal("Jane Doe", result.Items[0].UserName);
        Assert.Equal(2026, result.Items[0].IsoYear);
        Assert.Equal(21, result.Items[0].IsoWeek);
        Assert.Equal(0m, result.Items[0].TotalHours);
        Assert.Equal(1, result.Total);
    }

    [Fact]
    public async Task GetPendingApprovals_ExcludesDraftAndApproved()
    {
        await SeedCallerAsAsync(UserRole.Admin);
        var userId = await SeedUserAsync("Test", "User");

        await SeedWeekWithStatusAsync(userId, 2026, 20, TimesheetStatus.Draft);
        await SeedWeekWithStatusAsync(userId, 2026, 21, TimesheetStatus.Submitted);
        await SeedWeekWithStatusAsync(userId, 2026, 22, TimesheetStatus.Approved);

        var response = await Client.GetAsync("/api/timesheet-weeks/pending-approvals");

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<KeysetPage<PendingApprovalDto>>(Json);
        Assert.NotNull(result);
        Assert.Single(result.Items);
        Assert.Equal(21, result.Items[0].IsoWeek);
    }

    [Fact]
    public async Task GetPendingApprovals_Search_FiltersByUserName()
    {
        await SeedCallerAsAsync(UserRole.Admin);
        var aliceId = await SeedUserAsync("Alice", "Smith");
        var bobId = await SeedUserAsync("Bob", "Jones");
        await SeedWeekWithStatusAsync(aliceId, 2026, 21, TimesheetStatus.Submitted);
        await SeedWeekWithStatusAsync(bobId, 2026, 21, TimesheetStatus.Submitted);

        var response = await Client.GetAsync("/api/timesheet-weeks/pending-approvals?search=alice");

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<KeysetPage<PendingApprovalDto>>(Json);
        Assert.NotNull(result);
        Assert.Single(result.Items);
        Assert.Equal("Alice Smith", result.Items[0].UserName);
    }

    [Fact]
    public async Task GetPendingApprovals_Search_MatchesWeekNumber()
    {
        await SeedCallerAsAsync(UserRole.Admin);
        var userId = await SeedUserAsync("Alice", "Smith");
        await SeedWeekWithStatusAsync(userId, 2026, 20, TimesheetStatus.Submitted);
        await SeedWeekWithStatusAsync(userId, 2026, 21, TimesheetStatus.Submitted);

        var response = await Client.GetAsync("/api/timesheet-weeks/pending-approvals?search=21");

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<KeysetPage<PendingApprovalDto>>(Json);
        Assert.NotNull(result);
        Assert.Single(result.Items);
        Assert.Equal(21, result.Items[0].IsoWeek);
    }

    [Fact]
    public async Task GetPendingApprovals_DateRange_FiltersByOverlap()
    {
        await SeedCallerAsAsync(UserRole.Admin);
        var userId = await SeedUserAsync("Alice", "Smith");
        await SeedWeekWithStatusAsync(userId, 2026, 20, TimesheetStatus.Submitted);
        await SeedWeekWithStatusAsync(userId, 2026, 21, TimesheetStatus.Submitted);
        await SeedWeekWithStatusAsync(userId, 2026, 22, TimesheetStatus.Submitted);

        // W21 = 2026-05-18 → 2026-05-24. Range 18..24 selects W21 only.
        var response = await Client.GetAsync(
            "/api/timesheet-weeks/pending-approvals?dateFrom=2026-05-18&dateTo=2026-05-24");

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<KeysetPage<PendingApprovalDto>>(Json);
        Assert.NotNull(result);
        Assert.Single(result.Items);
        Assert.Equal(21, result.Items[0].IsoWeek);
    }

    [Fact]
    public async Task GetPendingApprovals_SortByEmployee_OrdersAscending()
    {
        await SeedCallerAsAsync(UserRole.Admin);
        var zachId = await SeedUserAsync("Zach", "Adams");
        var aliceId = await SeedUserAsync("Alice", "Brown");
        await SeedWeekWithStatusAsync(zachId, 2026, 21, TimesheetStatus.Submitted);
        await SeedWeekWithStatusAsync(aliceId, 2026, 21, TimesheetStatus.Submitted);

        var response = await Client.GetAsync("/api/timesheet-weeks/pending-approvals?sortBy=employee&sortDir=asc");

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<KeysetPage<PendingApprovalDto>>(Json);
        Assert.NotNull(result);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal("Alice Brown", result.Items[0].UserName);
        Assert.Equal("Zach Adams", result.Items[1].UserName);
    }
}
