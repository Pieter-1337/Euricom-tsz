using System.Net;
using System.Net.Http.Json;
using Tsz.Api.Tests.Integration.TestAuth;
using Tsz.Modules.LeaveTypes.Domain.LeaveTypes;
using Tsz.Modules.LeaveTypes.Domain.Leaves;
using Tsz.Modules.Timesheets.Domain.Timesheets;
using Tsz.Modules.Timesheets.Features;
using Tsz.Modules.Users.Contracts;
using Tsz.Modules.Users.Domain.Users;

namespace Tsz.Api.Tests.Integration;

public class LeaveBookingsForYearEndpointsTests : IntegrationTestBase, IAsyncLifetime
{
    private const string CallerOid = "test-user-id";

    // Seeded leave type IDs (from LeaveTypeConfiguration.HasData)
    private static readonly Guid VerlofId = new("11111111-1111-1111-1111-000000000001");

    public LeaveBookingsForYearEndpointsTests(TestWebApplicationFactory factory) : base(factory) { }

    public async Task InitializeAsync()
    {
        await WithUowAsync(uow => uow.RepositoryFor<TimesheetWeek>().BatchHardDeleteAsync(_ => true));
        await WithUowAsync(uow => uow.RepositoryFor<UserLeave>().BatchHardDeleteAsync(_ => true));
        await WithUowAsync(uow => uow.RepositoryFor<User>().BatchHardDeleteAsync(_ => true));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<Guid> SeedCallerAsAsync(UserRole role)
    {
        var id = Guid.NewGuid();
        await WithUowAsync(async uow =>
        {
            var user = User.Create("Caller", "User", $"caller-{id}@test.com", [role]);
            user.Id = id;
            user.LinkEntraOid(CallerOid);
            uow.RepositoryFor<User>().Add(user);
            await uow.SaveChangesAsync();
        });
        return id;
    }

    private async Task<Guid> SeedOtherUserAsync()
    {
        var id = Guid.NewGuid();
        await WithUowAsync(async uow =>
        {
            var user = User.Create("Other", "User", $"other-{id}@test.com", [UserRole.User]);
            user.Id = id;
            uow.RepositoryFor<User>().Add(user);
            await uow.SaveChangesAsync();
        });
        return id;
    }

    private async Task SeedLeaveWeekAsync(Guid userId, DateOnly date, decimal hours)
    {
        await WithUowAsync(async uow =>
        {
            // Determine IsoYear/IsoWeek from date
            var cal = System.Globalization.ISOWeek.GetYear(date.ToDateTime(TimeOnly.MinValue));
            var wk = System.Globalization.ISOWeek.GetWeekOfYear(date.ToDateTime(TimeOnly.MinValue));

            var week = TimesheetWeek.Create(userId, cal, wk);
            week.ApplyBookings([], [new LeaveBookingDto(VerlofId, date, hours)]);
            uow.RepositoryFor<TimesheetWeek>().Add(week);
            await uow.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task GetLeaveBookingsForYear_AsOwner_Returns200()
    {
        var userId = await SeedCallerAsAsync(UserRole.User);
        await SeedLeaveWeekAsync(userId, new DateOnly(2026, 5, 18), 8m);

        var response = await Client.GetAsync($"/api/timesheet-weeks/{userId}/leave-bookings?year=2026");

        response.EnsureSuccessStatusCode();
        var dtos = await response.Content.ReadFromJsonAsync<LeaveBookingForYearDto[]>(Json);
        Assert.NotNull(dtos);
        Assert.Single(dtos);
        Assert.Equal(new DateOnly(2026, 5, 18), dtos[0].Date);
        Assert.Equal(VerlofId, dtos[0].LeaveTypeId);
        Assert.Equal(8m, dtos[0].DurationHours);
    }

    [Fact]
    public async Task GetLeaveBookingsForYear_AsAdmin_Returns200()
    {
        var adminId = await SeedCallerAsAsync(UserRole.Admin);
        var otherUserId = await SeedOtherUserAsync();
        await SeedLeaveWeekAsync(otherUserId, new DateOnly(2026, 5, 18), 8m);

        var response = await Client.GetAsync($"/api/timesheet-weeks/{otherUserId}/leave-bookings?year=2026");

        response.EnsureSuccessStatusCode();
        var dtos = await response.Content.ReadFromJsonAsync<LeaveBookingForYearDto[]>(Json);
        Assert.NotNull(dtos);
        Assert.Single(dtos);
    }

    [Fact]
    public async Task GetLeaveBookingsForYear_AsOtherNonAdmin_Returns403()
    {
        await SeedCallerAsAsync(UserRole.User); // caller is non-admin
        var otherUserId = await SeedOtherUserAsync();
        await SeedLeaveWeekAsync(otherUserId, new DateOnly(2026, 5, 18), 8m);

        var response = await Client.GetAsync($"/api/timesheet-weeks/{otherUserId}/leave-bookings?year=2026");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetLeaveBookingsForYear_EmptyYear_ReturnsEmptyArray()
    {
        var userId = await SeedCallerAsAsync(UserRole.User);

        var response = await Client.GetAsync($"/api/timesheet-weeks/{userId}/leave-bookings?year=2026");

        response.EnsureSuccessStatusCode();
        var dtos = await response.Content.ReadFromJsonAsync<LeaveBookingForYearDto[]>(Json);
        Assert.NotNull(dtos);
        Assert.Empty(dtos);
    }

    [Fact]
    public async Task GetLeaveBookingsForYear_YearFilter_ExcludesOtherYear()
    {
        var userId = await SeedCallerAsAsync(UserRole.User);
        // Booking in 2026
        await SeedLeaveWeekAsync(userId, new DateOnly(2026, 5, 18), 8m);
        // Booking in 2027
        await SeedLeaveWeekAsync(userId, new DateOnly(2027, 3, 1), 4m);

        var response = await Client.GetAsync($"/api/timesheet-weeks/{userId}/leave-bookings?year=2026");

        response.EnsureSuccessStatusCode();
        var dtos = await response.Content.ReadFromJsonAsync<LeaveBookingForYearDto[]>(Json);
        Assert.NotNull(dtos);
        Assert.All(dtos, dto => Assert.Equal(2026, dto.Date.Year));
    }

    [Fact]
    public async Task GetLeaveBookingsForYear_SortedByDateThenLeaveTypeId()
    {
        var userId = await SeedCallerAsAsync(UserRole.User);
        await SeedLeaveWeekAsync(userId, new DateOnly(2026, 5, 20), 4m);
        await SeedLeaveWeekAsync(userId, new DateOnly(2026, 5, 18), 8m);

        var response = await Client.GetAsync($"/api/timesheet-weeks/{userId}/leave-bookings?year=2026");

        response.EnsureSuccessStatusCode();
        var dtos = await response.Content.ReadFromJsonAsync<LeaveBookingForYearDto[]>(Json);
        Assert.NotNull(dtos);
        Assert.Equal(2, dtos.Length);
        Assert.True(dtos[0].Date <= dtos[1].Date, "Results should be sorted by date ascending");
    }

    [Fact]
    public async Task GetLeaveBookingsForYear_LeaveTypeNameProjected()
    {
        var userId = await SeedCallerAsAsync(UserRole.User);
        await SeedLeaveWeekAsync(userId, new DateOnly(2026, 5, 18), 8m);

        var response = await Client.GetAsync($"/api/timesheet-weeks/{userId}/leave-bookings?year=2026");

        response.EnsureSuccessStatusCode();
        var dtos = await response.Content.ReadFromJsonAsync<LeaveBookingForYearDto[]>(Json);
        Assert.NotNull(dtos);
        Assert.NotEmpty(dtos[0].LeaveTypeName);
    }
}
