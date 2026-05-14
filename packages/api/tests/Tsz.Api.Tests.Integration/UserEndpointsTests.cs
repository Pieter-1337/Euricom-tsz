using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tsz.Api.Modules.Users;
using Tsz.Api.Modules.Users.Features;
using Tsz.Api.Tests.Integration.TestAuth;

namespace Tsz.Api.Tests.Integration;

public class UserEndpointsTests : IntegrationTestBase, IAsyncLifetime
{
    private const string TestEmail = "test@test.com";
    private const string TestOid = "test-user-id";

    public UserEndpointsTests(TestWebApplicationFactory factory) : base(factory) { }

    public Task InitializeAsync() => WithUowAsync(uow =>
        uow.RepositoryFor<User>().BatchHardDeleteAsync(_ => true));

    public Task DisposeAsync() => Task.CompletedTask;

    private Task SeedUserAsync(string email, UserRole role, string? oid = null) => WithUowAsync(async uow =>
    {
        var user = User.Create($"User_{Guid.NewGuid().ToString()[..6]}", email, role);
        if (oid is not null) user.LinkEntraOid(oid);
        uow.RepositoryFor<User>().Add(user);
        await uow.SaveChangesAsync();
    });

    private Task<User?> GetUserByEmailAsync(string email) => WithUowAsync(uow =>
        uow.RepositoryFor<User>().FirstOrDefaultAsync(u => u.Email == email, ignoreQueryFilters: true));

    [Fact]
    public async Task GetMe_NotProvisioned_ReturnsNotFound()
    {
        var response = await Client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_ProvisionedByOid_ReturnsOk()
    {
        await SeedUserAsync(TestEmail, UserRole.User, oid: TestOid);

        var response = await Client.GetAsync("/api/users/me");

        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<UserDto>(Json);
        Assert.NotNull(dto);
        Assert.Equal(TestEmail, dto.Email);
    }

    [Fact]
    public async Task GetMe_ProvisionedByEmailOnly_LinksOid_AndReturnsOk()
    {
        await SeedUserAsync(TestEmail, UserRole.User, oid: null);

        var response = await Client.GetAsync("/api/users/me");

        response.EnsureSuccessStatusCode();
        var linked = await GetUserByEmailAsync(TestEmail);
        Assert.NotNull(linked);
        Assert.Equal(TestOid, linked.EntraOid);
    }

    [Fact]
    public async Task GetUsers_AsAdmin_ReturnsOk()
    {
        await SeedUserAsync(TestEmail, UserRole.Admin, oid: TestOid);

        var response = await Client.GetAsync("/api/users");

        response.EnsureSuccessStatusCode();
        var list = await response.Content.ReadFromJsonAsync<List<UserDto>>(Json);
        Assert.NotNull(list);
        Assert.Contains(list, u => u.Email == TestEmail);
    }

    [Fact]
    public async Task GetUsers_AsNonAdmin_ReturnsForbidden()
    {
        await SeedUserAsync(TestEmail, UserRole.User, oid: TestOid);

        var response = await Client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetUsers_NotProvisioned_ReturnsForbidden()
    {
        var response = await Client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateUser_AsAdmin_Valid_ReturnsCreated()
    {
        await SeedUserAsync(TestEmail, UserRole.Admin, oid: TestOid);
        var command = new CreateUserCommand("Jane", "jane@example.com", UserRole.User);

        var response = await Client.PostAsJsonAsync("/api/users", command);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<UserDto>(Json);
        Assert.NotNull(dto);
        Assert.Equal("Jane", dto.Name);
        Assert.Equal("jane@example.com", dto.Email);
        Assert.Equal(UserRole.User, dto.Role);
    }

    [Fact]
    public async Task CreateUser_AsAdmin_SeedsUserLeaveRows()
    {
        await SeedUserAsync(TestEmail, UserRole.Admin, oid: TestOid);
        var command = new CreateUserCommand("Jane", "jane@example.com", UserRole.User);

        var createResponse = await Client.PostAsJsonAsync("/api/users", command);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var dto = await createResponse.Content.ReadFromJsonAsync<UserDto>(Json);
        Assert.NotNull(dto);

        var leavesResponse = await Client.GetAsync($"/api/users/{dto.Id}/leaves");
        leavesResponse.EnsureSuccessStatusCode();
        var leaves = await leavesResponse.Content.ReadFromJsonAsync<List<UserLeaveDto>>(Json);
        Assert.NotNull(leaves);
        Assert.Equal(4, leaves.Count);
    }

    [Fact]
    public async Task CreateUser_AsAdmin_Invalid_ReturnsBadRequest()
    {
        await SeedUserAsync(TestEmail, UserRole.Admin, oid: TestOid);
        var command = new CreateUserCommand("", "not-an-email", UserRole.User);

        var response = await Client.PostAsJsonAsync("/api/users", command);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateUser_DuplicateEmail_Returns409WithProblemDetails()
    {
        await SeedUserAsync(TestEmail, UserRole.Admin, oid: TestOid);
        var command = new CreateUserCommand("Jane", "dup@example.com", UserRole.User);
        var first = await Client.PostAsJsonAsync("/api/users", command);
        first.EnsureSuccessStatusCode();

        var second = await Client.PostAsJsonAsync("/api/users", command);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Contains("application/problem+json", second.Content.Headers.ContentType?.MediaType);

        var body = await second.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("ERR_USER_EMAIL_ALREADY_EXISTS", body.GetProperty("code").GetString());

        var emailErrors = body.GetProperty("errors").GetProperty("Email");
        Assert.Equal("ERR_USER_EMAIL_ALREADY_EXISTS", emailErrors[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task CreateUser_InvalidEmail_Returns400WithFieldErrors()
    {
        await SeedUserAsync(TestEmail, UserRole.Admin, oid: TestOid);
        var command = new CreateUserCommand("Jane", "not-an-email", UserRole.User);

        var response = await Client.PostAsJsonAsync("/api/users", command);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var emailErrors = body.GetProperty("errors").GetProperty("Email");
        Assert.True(emailErrors.GetArrayLength() > 0);
    }

    [Fact]
    public async Task UpdateUser_Existing_ReturnsOk()
    {
        await SeedUserAsync(TestEmail, UserRole.Admin, oid: TestOid);
        var created = await Client.PostAsJsonAsync("/api/users",
            new CreateUserCommand("Old", "u@example.com", UserRole.User));
        var dto = (await created.Content.ReadFromJsonAsync<UserDto>(Json))!;

        var update = new UpdateUserCommand(dto.Id, "New", UserRole.ClientManager);
        var response = await Client.PutAsJsonAsync($"/api/users/{dto.Id}", update);

        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<UserDto>(Json);
        Assert.NotNull(updated);
        Assert.Equal("New", updated.Name);
        Assert.Equal(UserRole.ClientManager, updated.Role);
    }

    [Fact]
    public async Task UpdateUser_RouteIdMismatch_ReturnsBadRequest()
    {
        await SeedUserAsync(TestEmail, UserRole.Admin, oid: TestOid);
        var update = new UpdateUserCommand(Guid.NewGuid(), "x", UserRole.User);

        var response = await Client.PutAsJsonAsync($"/api/users/{Guid.NewGuid()}", update);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateUser_Missing_Returns404WithProblemDetails()
    {
        await SeedUserAsync(TestEmail, UserRole.Admin, oid: TestOid);
        var id = Guid.NewGuid();

        var response = await Client.PutAsJsonAsync($"/api/users/{id}",
            new UpdateUserCommand(id, "x", UserRole.User));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("ERR_USER_NOT_FOUND", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task DeleteUser_Existing_ReturnsNoContent_AndHidesFromList()
    {
        await SeedUserAsync(TestEmail, UserRole.Admin, oid: TestOid);
        var created = await Client.PostAsJsonAsync("/api/users",
            new CreateUserCommand("Doomed", "doomed@example.com", UserRole.User));
        var dto = (await created.Content.ReadFromJsonAsync<UserDto>(Json))!;

        var response = await Client.DeleteAsync($"/api/users/{dto.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var getResponse = await Client.GetAsync($"/api/users/{dto.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);

        var row = await WithUowAsync(uow =>
            uow.RepositoryFor<User>().FirstOrDefaultAsync(u => u.Id == dto.Id, ignoreQueryFilters: true));
        Assert.NotNull(row);
        Assert.NotNull(row.DeletedAt);
    }

    [Fact]
    public async Task DeleteUser_Missing_Returns404WithProblemDetails()
    {
        await SeedUserAsync(TestEmail, UserRole.Admin, oid: TestOid);

        var response = await Client.DeleteAsync($"/api/users/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("ERR_USER_NOT_FOUND", body.GetProperty("code").GetString());
    }
}
