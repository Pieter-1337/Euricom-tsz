using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tsz.Api.Modules.LeaveTypes;
using Tsz.Api.Modules.LeaveTypes.Features;
using Tsz.Api.Modules.Users;
using Tsz.Api.Tests.Integration.TestAuth;

namespace Tsz.Api.Tests.Integration;

public class LeaveTypeEndpointsTests : IntegrationTestBase, IAsyncLifetime
{
    private const string AdminEmail = "test@test.com";
    private const string AdminOid = "test-user-id";

    public LeaveTypeEndpointsTests(TestWebApplicationFactory factory) : base(factory) { }

    public Task InitializeAsync() => WithUowAsync(async uow =>
    {
        await uow.RepositoryFor<UserLeave>().BatchHardDeleteAsync(_ => true);
        await uow.RepositoryFor<User>().BatchHardDeleteAsync(_ => true);
        await uow.RepositoryFor<LeaveType>().BatchHardDeleteAsync(_ => true);
    });

    public Task DisposeAsync() => Task.CompletedTask;

    private Task SeedAuthedUserAsync() => WithUowAsync(async uow =>
    {
        var user = User.Create("Authed", AdminEmail, UserRole.Admin);
        user.LinkEntraOid(AdminOid);
        uow.RepositoryFor<User>().Add(user);
        await uow.SaveChangesAsync();
    });

    [Fact]
    public async Task GetLeaveTypes_ReturnsOk()
    {
        await SeedAuthedUserAsync();
        await WithUowAsync(async uow =>
        {
            uow.RepositoryFor<LeaveType>().Add(LeaveType.Create("Verlof", LeaveAllowed.Limited, 20m));
            uow.RepositoryFor<LeaveType>().Add(LeaveType.Create("Ziekte", LeaveAllowed.Unlimited, null));
            await uow.SaveChangesAsync();
        });

        var response = await Client.GetAsync("/api/leave-types");

        response.EnsureSuccessStatusCode();
        var list = await response.Content.ReadFromJsonAsync<List<LeaveTypeDto>>(Json);
        Assert.NotNull(list);
        Assert.Contains(list, x => x.Name == "Verlof" && x.DefaultAllowed == LeaveAllowed.Limited);
        Assert.Contains(list, x => x.Name == "Ziekte" && x.DefaultAllowed == LeaveAllowed.Unlimited);
    }

    [Fact]
    public async Task CreateLeaveType_Valid_ReturnsCreated()
    {
        await SeedAuthedUserAsync();
        var command = new CreateLeaveTypeCommand("NewType", LeaveAllowed.Limited, 15m, null, null, null, null);

        var response = await Client.PostAsJsonAsync("/api/leave-types", command);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<LeaveTypeDto>(Json);
        Assert.NotNull(dto);
        Assert.Equal("NewType", dto.Name);
    }

    [Fact]
    public async Task CreateLeaveType_DuplicateName_Returns409WithProblemDetails()
    {
        await SeedAuthedUserAsync();
        var command = new CreateLeaveTypeCommand("DupType", LeaveAllowed.Limited, 10m, null, null, null, null);
        (await Client.PostAsJsonAsync("/api/leave-types", command)).EnsureSuccessStatusCode();

        var second = await Client.PostAsJsonAsync("/api/leave-types", command);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Contains("application/problem+json", second.Content.Headers.ContentType?.MediaType);

        var body = await second.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("ERR_LEAVE_TYPE_NAME_ALREADY_EXISTS", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task UpdateLeaveType_Existing_ReturnsOk()
    {
        await SeedAuthedUserAsync();
        var created = await Client.PostAsJsonAsync("/api/leave-types",
            new CreateLeaveTypeCommand("ToUpdate", LeaveAllowed.Limited, 10m, null, null, null, null));
        var dto = (await created.Content.ReadFromJsonAsync<LeaveTypeDto>(Json))!;

        var update = new UpdateLeaveTypeCommand(dto.Id, "Updated", LeaveAllowed.Unlimited, null, null, null, null, null);
        var response = await Client.PutAsJsonAsync($"/api/leave-types/{dto.Id}", update);

        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<LeaveTypeDto>(Json);
        Assert.NotNull(updated);
        Assert.Equal("Updated", updated.Name);
    }

    [Fact]
    public async Task UpdateLeaveType_Missing_Returns404WithProblemDetails()
    {
        await SeedAuthedUserAsync();
        var id = Guid.NewGuid();
        var update = new UpdateLeaveTypeCommand(id, "x", LeaveAllowed.Limited, null, null, null, null, null);

        var response = await Client.PutAsJsonAsync($"/api/leave-types/{id}", update);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("ERR_LEAVE_TYPE_NOT_FOUND", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task DeleteLeaveType_Existing_NotInUse_ReturnsNoContent()
    {
        await SeedAuthedUserAsync();
        var created = await Client.PostAsJsonAsync("/api/leave-types",
            new CreateLeaveTypeCommand("ToDelete", LeaveAllowed.Limited, 5m, null, null, null, null));
        var dto = (await created.Content.ReadFromJsonAsync<LeaveTypeDto>(Json))!;

        var response = await Client.DeleteAsync($"/api/leave-types/{dto.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteLeaveType_UnknownId_Returns404WithProblemDetails()
    {
        await SeedAuthedUserAsync();

        var response = await Client.DeleteAsync($"/api/leave-types/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("ERR_LEAVE_TYPE_NOT_FOUND", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task DeleteLeaveType_InUse_Returns409WithProblemDetails()
    {
        await SeedAuthedUserAsync();

        // Seed a leave type that is referenced by a UserLeave
        var ltId = Guid.Empty;
        await WithUowAsync(async uow =>
        {
            var lt = LeaveType.Create("InUseType", LeaveAllowed.Limited, 20m);
            uow.RepositoryFor<LeaveType>().Add(lt);
            ltId = lt.Id;

            var user = User.Create("SomeUser", $"u_{Guid.NewGuid().ToString()[..6]}@example.com", UserRole.User);
            uow.RepositoryFor<User>().Add(user);
            uow.RepositoryFor<UserLeave>().Add(UserLeave.Create(user.Id, lt.Id, 20m));

            await uow.SaveChangesAsync();
        });

        var response = await Client.DeleteAsync($"/api/leave-types/{ltId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("ERR_LEAVE_TYPE_IN_USE", body.GetProperty("code").GetString());
    }
}
