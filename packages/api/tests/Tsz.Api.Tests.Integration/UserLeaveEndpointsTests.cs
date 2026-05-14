using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tsz.Api.Modules.LeaveTypes;
using Tsz.Api.Modules.Users;
using Tsz.Api.Modules.Users.Features;
using Tsz.Api.Tests.Integration.TestAuth;

namespace Tsz.Api.Tests.Integration;

public class UserLeaveEndpointsTests : IntegrationTestBase, IAsyncLifetime
{
    private const string AdminEmail = "test@test.com";
    private const string AdminOid = "test-user-id";

    public UserLeaveEndpointsTests(TestWebApplicationFactory factory) : base(factory) { }

    public Task InitializeAsync() => WithUowAsync(async uow =>
    {
        await uow.RepositoryFor<UserLeave>().BatchHardDeleteAsync(_ => true);
        await uow.RepositoryFor<LeaveType>().BatchHardDeleteAsync(_ => true);
        await uow.RepositoryFor<User>().BatchHardDeleteAsync(_ => true);
    });

    public Task DisposeAsync() => Task.CompletedTask;

    private Task SeedAdminAsync() => WithUowAsync(async uow =>
    {
        var user = User.Create("Admin", AdminEmail, UserRole.Admin);
        user.LinkEntraOid(AdminOid);
        uow.RepositoryFor<User>().Add(user);
        await uow.SaveChangesAsync();
    });

    private async Task<(Guid userId, Guid leaveTypeId)> SeedUserAndLeaveTypeAsync(
        string ltName = "Verlof",
        LeaveAllowed allowed = LeaveAllowed.Limited,
        decimal? defaultDays = 10m)
    {
        var userId = Guid.Empty;
        var ltId = Guid.Empty;
        await WithUowAsync(async uow =>
        {
            var lt = LeaveType.Create(ltName, allowed, defaultDays);
            uow.RepositoryFor<LeaveType>().Add(lt);
            ltId = lt.Id;

            var user = User.Create("Test User", $"testuser_{Guid.NewGuid().ToString()[..6]}@example.com", UserRole.User);
            uow.RepositoryFor<User>().Add(user);
            userId = user.Id;

            uow.RepositoryFor<UserLeave>().Add(UserLeave.Create(userId, ltId, defaultDays));

            await uow.SaveChangesAsync();
        });
        return (userId, ltId);
    }

    [Fact]
    public async Task GetUserLeaves_ReturnsUsersRows()
    {
        await SeedAdminAsync();
        var (userId, ltId) = await SeedUserAndLeaveTypeAsync();

        var response = await Client.GetAsync($"/api/users/{userId}/leaves");

        response.EnsureSuccessStatusCode();
        var leaves = await response.Content.ReadFromJsonAsync<List<UserLeaveDto>>(Json);
        Assert.NotNull(leaves);
        Assert.Single(leaves);
        Assert.Equal(ltId, leaves[0].LeaveTypeId);
        Assert.Equal(10m, leaves[0].TotalDays);
        Assert.Equal(LeaveAllowed.Limited, leaves[0].DefaultAllowed);
        Assert.Equal("Verlof", leaves[0].LeaveTypeName);
    }

    [Fact]
    public async Task UpdateUserLeave_Limited_ReturnsOk()
    {
        await SeedAdminAsync();
        var (userId, ltId) = await SeedUserAndLeaveTypeAsync("UpdateTest");

        var list = await Client.GetFromJsonAsync<List<UserLeaveDto>>($"/api/users/{userId}/leaves", Json);
        var dto = list![0];

        var update = new UpdateUserLeaveCommand(userId, dto.Id, 25m);
        var response = await Client.PutAsJsonAsync($"/api/users/{userId}/leaves/{dto.Id}", update);

        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<UserLeaveDto>(Json);
        Assert.NotNull(updated);
        Assert.Equal(25m, updated.TotalDays);
        Assert.Equal(ltId, updated.LeaveTypeId);
    }

    [Fact]
    public async Task UpdateUserLeave_Unlimited_WithDays_ReturnsBadRequest()
    {
        await SeedAdminAsync();
        var (userId, _) = await SeedUserAndLeaveTypeAsync("Ziekte", LeaveAllowed.Unlimited, null);

        var list = await Client.GetFromJsonAsync<List<UserLeaveDto>>($"/api/users/{userId}/leaves", Json);
        var dto = list![0];

        var update = new UpdateUserLeaveCommand(userId, dto.Id, 5m);
        var response = await Client.PutAsJsonAsync($"/api/users/{userId}/leaves/{dto.Id}", update);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var errors = body.GetProperty("errors");
        Assert.True(errors.EnumerateObject().Any());
        var firstError = errors.EnumerateObject().First().Value[0];
        Assert.Equal("ERR_INVALID", firstError.GetProperty("code").GetString());
    }

    [Fact]
    public async Task UpdateUserLeave_Limited_NullDays_ReturnsBadRequest()
    {
        await SeedAdminAsync();
        var (userId, _) = await SeedUserAndLeaveTypeAsync();

        var list = await Client.GetFromJsonAsync<List<UserLeaveDto>>($"/api/users/{userId}/leaves", Json);
        var dto = list![0];

        var update = new UpdateUserLeaveCommand(userId, dto.Id, null);
        var response = await Client.PutAsJsonAsync($"/api/users/{userId}/leaves/{dto.Id}", update);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var errors = body.GetProperty("errors");
        Assert.True(errors.EnumerateObject().Any());
        var firstError = errors.EnumerateObject().First().Value[0];
        Assert.Equal("ERR_INVALID", firstError.GetProperty("code").GetString());
    }

    [Fact]
    public async Task UpdateUserLeave_Missing_Returns404WithProblemDetails()
    {
        await SeedAdminAsync();
        var userId = Guid.NewGuid();
        var id = Guid.NewGuid();
        var update = new UpdateUserLeaveCommand(userId, id, 5m);

        var response = await Client.PutAsJsonAsync($"/api/users/{userId}/leaves/{id}", update);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("ERR_USER_LEAVE_NOT_FOUND", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task AddUserLeave_NewCombination_Returns201WithDto_AndPersistsRow()
    {
        await SeedAdminAsync();
        Guid userId = Guid.Empty;
        Guid ltId = Guid.Empty;
        await WithUowAsync(async uow =>
        {
            var lt = LeaveType.Create("NewLeave", LeaveAllowed.Limited, 15m);
            uow.RepositoryFor<LeaveType>().Add(lt);
            ltId = lt.Id;

            var user = User.Create("Fresh User", $"fresh_{Guid.NewGuid().ToString()[..6]}@example.com", UserRole.User);
            uow.RepositoryFor<User>().Add(user);
            userId = user.Id;

            await uow.SaveChangesAsync();
        });

        var command = new AddUserLeaveCommand(userId, ltId, 15m);
        var response = await Client.PostAsJsonAsync($"/api/users/{userId}/leaves", command);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var dto = await response.Content.ReadFromJsonAsync<UserLeaveDto>(Json);
        Assert.NotNull(dto);
        Assert.NotEqual(Guid.Empty, dto.Id);
        Assert.Equal(ltId, dto.LeaveTypeId);
        Assert.Equal(15m, dto.TotalDays);
        Assert.Equal(LeaveAllowed.Limited, dto.DefaultAllowed);
        Assert.Equal("NewLeave", dto.LeaveTypeName);

        var persisted = await WithUowAsync(uow =>
            uow.RepositoryFor<UserLeave>().FirstOrDefaultAsync(ul => ul.Id == dto.Id));
        Assert.NotNull(persisted);
        Assert.Equal(ltId, persisted.LeaveTypeId);
        Assert.Equal(15m, persisted.TotalDays);
    }

    [Fact]
    public async Task AddUserLeave_DuplicateLeaveType_Returns409WithProblemDetails()
    {
        await SeedAdminAsync();
        var (userId, ltId) = await SeedUserAndLeaveTypeAsync("AddDupTest");

        // Try to add a second leave for the same user + leaveType
        var command = new AddUserLeaveCommand(userId, ltId, 5m);
        var response = await Client.PostAsJsonAsync($"/api/users/{userId}/leaves", command);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("ERR_USER_LEAVE_DUPLICATE", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task DeleteUserLeave_Existing_ReturnsNoContent()
    {
        await SeedAdminAsync();
        var (userId, _) = await SeedUserAndLeaveTypeAsync("DelTest");

        var list = await Client.GetFromJsonAsync<List<UserLeaveDto>>($"/api/users/{userId}/leaves", Json);
        var dto = list![0];

        var response = await Client.DeleteAsync($"/api/users/{userId}/leaves/{dto.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteUserLeave_Missing_Returns404WithProblemDetails()
    {
        await SeedAdminAsync();
        var userId = Guid.NewGuid();
        var id = Guid.NewGuid();

        var response = await Client.DeleteAsync($"/api/users/{userId}/leaves/{id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("ERR_USER_LEAVE_NOT_FOUND", body.GetProperty("code").GetString());
    }
}
