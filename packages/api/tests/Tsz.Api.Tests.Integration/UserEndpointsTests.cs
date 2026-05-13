using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tsz.Api.Common.Persistence;
using Tsz.Api.Modules.Users;
using Tsz.Api.Tests.Integration.TestAuth;

namespace Tsz.Api.Tests.Integration;

public class UserEndpointsTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private const string TestEmail = "test@test.com";
    private const string TestOid = "test-user-id";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public UserEndpointsTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = await db.Users.IgnoreQueryFilters().ToListAsync();
        db.Users.RemoveRange(users);
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task SeedUserAsync(string email, UserRole role, string? oid = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = User.Create($"User_{Guid.NewGuid().ToString()[..6]}", email, role);
        if (oid is not null) user.LinkEntraOid(oid);
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }

    private async Task<User?> GetUserByEmailAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == email);
    }

    [Fact]
    public async Task GetMe_NotProvisioned_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_ProvisionedByOid_ReturnsOk()
    {
        await SeedUserAsync(TestEmail, UserRole.User, oid: TestOid);

        var response = await _client.GetAsync("/api/users/me");

        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<UserDto>(Json);
        Assert.NotNull(dto);
        Assert.Equal(TestEmail, dto.Email);
    }

    [Fact]
    public async Task GetMe_ProvisionedByEmailOnly_LinksOid_AndReturnsOk()
    {
        await SeedUserAsync(TestEmail, UserRole.User, oid: null);

        var response = await _client.GetAsync("/api/users/me");

        response.EnsureSuccessStatusCode();
        var linked = await GetUserByEmailAsync(TestEmail);
        Assert.NotNull(linked);
        Assert.Equal(TestOid, linked.EntraOid);
    }

    [Fact]
    public async Task GetUsers_AsAdmin_ReturnsOk()
    {
        await SeedUserAsync(TestEmail, UserRole.Admin, oid: TestOid);

        var response = await _client.GetAsync("/api/users");

        response.EnsureSuccessStatusCode();
        var list = await response.Content.ReadFromJsonAsync<List<UserDto>>(Json);
        Assert.NotNull(list);
        Assert.Contains(list, u => u.Email == TestEmail);
    }

    [Fact]
    public async Task GetUsers_AsNonAdmin_ReturnsForbidden()
    {
        await SeedUserAsync(TestEmail, UserRole.User, oid: TestOid);

        var response = await _client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetUsers_NotProvisioned_ReturnsForbidden()
    {
        var response = await _client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateUser_AsAdmin_Valid_ReturnsCreated_WithLeaveDefaults()
    {
        await SeedUserAsync(TestEmail, UserRole.Admin, oid: TestOid);
        var command = new CreateUserCommand("Jane", "jane@example.com", UserRole.User);

        var response = await _client.PostAsJsonAsync("/api/users", command);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<UserDto>(Json);
        Assert.NotNull(dto);
        Assert.Equal("Jane", dto.Name);
        Assert.Equal("jane@example.com", dto.Email);
        Assert.Equal(UserRole.User, dto.Role);
        Assert.Equal(User.DefaultHolidayDays, dto.HolidayDays);
        Assert.Equal(User.DefaultAdvDays, dto.AdvDays);
        Assert.Equal(User.DefaultAncienniteitDays, dto.AncienniteitDays);
        Assert.Equal(User.DefaultSicknessDays, dto.SicknessDays);
    }

    [Fact]
    public async Task CreateUser_AsAdmin_Invalid_ReturnsBadRequest()
    {
        await SeedUserAsync(TestEmail, UserRole.Admin, oid: TestOid);
        var command = new CreateUserCommand("", "not-an-email", UserRole.User);

        var response = await _client.PostAsJsonAsync("/api/users", command);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateUser_DuplicateEmail_ReturnsConflict()
    {
        await SeedUserAsync(TestEmail, UserRole.Admin, oid: TestOid);
        var command = new CreateUserCommand("Jane", "dup@example.com", UserRole.User);
        var first = await _client.PostAsJsonAsync("/api/users", command);
        first.EnsureSuccessStatusCode();

        var second = await _client.PostAsJsonAsync("/api/users", command);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task UpdateUser_Existing_ReturnsOk()
    {
        await SeedUserAsync(TestEmail, UserRole.Admin, oid: TestOid);
        var created = await _client.PostAsJsonAsync("/api/users",
            new CreateUserCommand("Old", "u@example.com", UserRole.User));
        var dto = (await created.Content.ReadFromJsonAsync<UserDto>(Json))!;

        var update = new UpdateUserCommand(dto.Id, "New", UserRole.ClientManager);
        var response = await _client.PutAsJsonAsync($"/api/users/{dto.Id}", update);

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

        var response = await _client.PutAsJsonAsync($"/api/users/{Guid.NewGuid()}", update);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateUser_Missing_ReturnsNotFound()
    {
        await SeedUserAsync(TestEmail, UserRole.Admin, oid: TestOid);
        var id = Guid.NewGuid();

        var response = await _client.PutAsJsonAsync($"/api/users/{id}",
            new UpdateUserCommand(id, "x", UserRole.User));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_Existing_ReturnsNoContent_AndHidesFromList()
    {
        await SeedUserAsync(TestEmail, UserRole.Admin, oid: TestOid);
        var created = await _client.PostAsJsonAsync("/api/users",
            new CreateUserCommand("Doomed", "doomed@example.com", UserRole.User));
        var dto = (await created.Content.ReadFromJsonAsync<UserDto>(Json))!;

        var response = await _client.DeleteAsync($"/api/users/{dto.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var getResponse = await _client.GetAsync($"/api/users/{dto.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == dto.Id);
        Assert.NotNull(row);
        Assert.NotNull(row.DeletedAt);
    }

    [Fact]
    public async Task DeleteUser_Missing_ReturnsNotFound()
    {
        await SeedUserAsync(TestEmail, UserRole.Admin, oid: TestOid);

        var response = await _client.DeleteAsync($"/api/users/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
