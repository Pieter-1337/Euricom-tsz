using System.Net;
using System.Net.Http.Json;
using Tsz.Api.Tests.Integration.TestAuth;
using Tsz.Modules.Timesheets.Domain.Holidays;
using Tsz.Modules.Timesheets.Features;
using Tsz.Modules.Users.Contracts;
using Tsz.Modules.Users.Domain.Users;

namespace Tsz.Api.Tests.Integration;

public class HolidaysEndpointsTests : IntegrationTestBase, IAsyncLifetime
{
    private const string CallerOid = "test-user-id";

    public HolidaysEndpointsTests(TestWebApplicationFactory factory) : base(factory) { }

    public async Task InitializeAsync()
    {
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

    [Fact]
    public async Task GetHolidays_2026_Returns10BelgianHolidays()
    {
        await SeedCallerAsAsync(UserRole.User);

        var response = await Client.GetAsync("/api/workdays/holidays?year=2026");

        response.EnsureSuccessStatusCode();
        var dtos = await response.Content.ReadFromJsonAsync<HolidayDto[]>(Json);
        Assert.NotNull(dtos);
        Assert.Equal(10, dtos.Length);
        Assert.All(dtos, dto => Assert.Equal(2026, dto.Date.Year));
    }

    [Fact]
    public async Task GetHolidays_2027_Returns10BelgianHolidays()
    {
        await SeedCallerAsAsync(UserRole.User);

        var response = await Client.GetAsync("/api/workdays/holidays?year=2027");

        response.EnsureSuccessStatusCode();
        var dtos = await response.Content.ReadFromJsonAsync<HolidayDto[]>(Json);
        Assert.NotNull(dtos);
        Assert.Equal(10, dtos.Length);
        Assert.All(dtos, dto => Assert.Equal(2027, dto.Date.Year));
    }

    [Fact]
    public async Task GetHolidays_2028_Returns10BelgianHolidays()
    {
        await SeedCallerAsAsync(UserRole.User);

        var response = await Client.GetAsync("/api/workdays/holidays?year=2028");

        response.EnsureSuccessStatusCode();
        var dtos = await response.Content.ReadFromJsonAsync<HolidayDto[]>(Json);
        Assert.NotNull(dtos);
        Assert.Equal(10, dtos.Length);
        Assert.All(dtos, dto => Assert.Equal(2028, dto.Date.Year));
    }

    [Fact]
    public async Task GetHolidays_CorrectShape_DateNameType()
    {
        await SeedCallerAsAsync(UserRole.User);

        var response = await Client.GetAsync("/api/workdays/holidays?year=2026");

        response.EnsureSuccessStatusCode();
        var dtos = await response.Content.ReadFromJsonAsync<HolidayDto[]>(Json);
        Assert.NotNull(dtos);
        var newYear = dtos.FirstOrDefault(d => d.Date == new DateOnly(2026, 1, 1));
        Assert.NotNull(newYear);
        Assert.Equal("New Year's Day", newYear.Name);
        Assert.Equal(HolidayType.Public, newYear.Type);
    }

    [Fact]
    public async Task GetHolidays_AnyAuthenticatedUser_Returns200()
    {
        // Regular User (not Admin) can access the holidays endpoint.
        await SeedCallerAsAsync(UserRole.User);

        var response = await Client.GetAsync("/api/workdays/holidays?year=2026");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task GetHolidays_EmptyYear_ReturnsEmptyArray()
    {
        await SeedCallerAsAsync(UserRole.User);

        var response = await Client.GetAsync("/api/workdays/holidays?year=2030");

        response.EnsureSuccessStatusCode();
        var dtos = await response.Content.ReadFromJsonAsync<HolidayDto[]>(Json);
        Assert.NotNull(dtos);
        Assert.Empty(dtos);
    }

    [Fact]
    public async Task GetHolidays_SortedByDate()
    {
        await SeedCallerAsAsync(UserRole.User);

        var response = await Client.GetAsync("/api/workdays/holidays?year=2026");

        response.EnsureSuccessStatusCode();
        var dtos = await response.Content.ReadFromJsonAsync<HolidayDto[]>(Json);
        Assert.NotNull(dtos);
        for (var i = 1; i < dtos.Length; i++)
            Assert.True(dtos[i - 1].Date <= dtos[i].Date, $"Holiday at index {i - 1} should come before index {i}");
    }
}
