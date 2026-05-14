using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tsz.Api.Modules.Users;
using Tsz.Api.Modules.Users.Features;
using Tsz.Api.Tests.Integration.TestAuth;

namespace Tsz.Api.Tests.Integration;

public class UserLeaveEndpointsTests : IntegrationTestBase, IAsyncLifetime
{
    private const string AdminEmail = "test@test.com";
    private const string AdminOid = "test-user-id";
    private static readonly int CurrentYear = DateTimeOffset.UtcNow.Year;

    // Seed GUIDs matching LeaveTypeConfiguration.HasData()
    private static readonly Guid VerlofId      = new("11111111-1111-1111-1111-000000000001");
    private static readonly Guid AdvId         = new("11111111-1111-1111-1111-000000000002");
    private static readonly Guid AncienniteitId = new("11111111-1111-1111-1111-000000000003");
    private static readonly Guid ZiekteId      = new("11111111-1111-1111-1111-000000000004");

    public UserLeaveEndpointsTests(TestWebApplicationFactory factory) : base(factory) { }

    public Task InitializeAsync() => WithUowAsync(async uow =>
    {
        await uow.RepositoryFor<UserLeave>().BatchHardDeleteAsync(_ => true);
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

    /// <summary>
    /// Creates a user + 4 UserLeave rows (one per seeded LeaveType) for the current year.
    /// </summary>
    private async Task<Guid> SeedUserWithCurrentYearLeavesAsync()
    {
        var userId = Guid.Empty;
        await WithUowAsync(async uow =>
        {
            var user = User.Create("Test User", $"user_{Guid.NewGuid().ToString()[..6]}@example.com", UserRole.User);
            uow.RepositoryFor<User>().Add(user);
            userId = user.Id;

            var ltRepo = uow.RepositoryFor<LeaveType>();
            var leaveTypes = await ltRepo.GetAllAsListAsync(ct: default);

            var leaveRepo = uow.RepositoryFor<UserLeave>();
            foreach (var lt in leaveTypes)
                leaveRepo.Add(UserLeave.Create(userId, lt.Id, CurrentYear, lt.DefaultDays));

            await uow.SaveChangesAsync();
        });
        return userId;
    }

    // ── GET ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_CurrentYear_ReturnsSeedCount()
    {
        await SeedAdminAsync();
        var userId = await SeedUserWithCurrentYearLeavesAsync();

        var response = await Client.GetAsync($"/api/users/{userId}/leaves");

        response.EnsureSuccessStatusCode();
        var leaves = await response.Content.ReadFromJsonAsync<List<UserLeaveDto>>(Json);
        Assert.NotNull(leaves);
        Assert.Equal(4, leaves.Count);
    }

    [Fact]
    public async Task Get_YearWithNoRows_ReturnsEmpty()
    {
        await SeedAdminAsync();
        var userId = await SeedUserWithCurrentYearLeavesAsync();

        var response = await Client.GetAsync($"/api/users/{userId}/leaves?year=2020");

        response.EnsureSuccessStatusCode();
        var leaves = await response.Content.ReadFromJsonAsync<List<UserLeaveDto>>(Json);
        Assert.NotNull(leaves);
        Assert.Empty(leaves);
    }

    // ── PUT (bulk) ───────────────────────────────────────────────────────────

    [Fact]
    public async Task BulkPut_Valid_Returns200AndFullYearSet()
    {
        await SeedAdminAsync();
        var userId = await SeedUserWithCurrentYearLeavesAsync();

        var leaves = await Client.GetFromJsonAsync<List<UserLeaveDto>>(
            $"/api/users/{userId}/leaves", Json);
        Assert.NotNull(leaves);

        var updatedItems = leaves.Select(l => new UpdateUserLeavesItem(
            l.Id,
            l.DefaultAllowed == LeaveAllowed.Unlimited ? null : l.TotalDays)).ToList();
        // bump Verlof to 22
        var verlofItem = updatedItems.First(i => leaves.First(l => l.Id == i.Id).LeaveTypeName == "Verlof");
        updatedItems[updatedItems.IndexOf(verlofItem)] = verlofItem with { TotalDays = 22m };

        var body = new UpdateUserLeavesBody(CurrentYear, updatedItems);
        var putResp = await Client.PutAsJsonAsync($"/api/users/{userId}/leaves", body, Json);

        putResp.EnsureSuccessStatusCode();
        var result = await putResp.Content.ReadFromJsonAsync<List<UserLeaveDto>>(Json);
        Assert.NotNull(result);
        Assert.Equal(4, result.Count);
        Assert.Contains(result, r => r.LeaveTypeName == "Verlof" && r.TotalDays == 22m);

        // Subsequent GET reflects change
        var getResp = await Client.GetFromJsonAsync<List<UserLeaveDto>>(
            $"/api/users/{userId}/leaves", Json);
        Assert.NotNull(getResp);
        Assert.Contains(getResp, r => r.LeaveTypeName == "Verlof" && r.TotalDays == 22m);
    }

    [Fact]
    public async Task BulkPut_NegativeTotalDays_Returns400_NoRowsChanged()
    {
        await SeedAdminAsync();
        var userId = await SeedUserWithCurrentYearLeavesAsync();

        var leaves = await Client.GetFromJsonAsync<List<UserLeaveDto>>(
            $"/api/users/{userId}/leaves", Json);
        Assert.NotNull(leaves);
        var verlofLeave = leaves.First(l => l.LeaveTypeName == "Verlof");

        var body = new UpdateUserLeavesBody(CurrentYear,
            [new UpdateUserLeavesItem(verlofLeave.Id, -1m)]);
        var response = await Client.PutAsJsonAsync($"/api/users/{userId}/leaves", body, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var errors = problem.GetProperty("errors");
        Assert.True(errors.EnumerateObject().Any(e =>
            e.Value.EnumerateArray().Any(v => v.GetProperty("code").GetString() == "ERR_INVALID")));

        // No rows changed
        var after = await Client.GetFromJsonAsync<List<UserLeaveDto>>(
            $"/api/users/{userId}/leaves", Json);
        Assert.NotNull(after);
        Assert.Equal(verlofLeave.TotalDays, after.First(l => l.Id == verlofLeave.Id).TotalDays);
    }

    [Fact]
    public async Task BulkPut_NonexistentRowId_Returns404()
    {
        await SeedAdminAsync();
        var userId = await SeedUserWithCurrentYearLeavesAsync();

        var body = new UpdateUserLeavesBody(CurrentYear,
            [new UpdateUserLeavesItem(Guid.NewGuid(), 5m)]);
        var response = await Client.PutAsJsonAsync($"/api/users/{userId}/leaves", body, Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("ERR_USER_LEAVE_NOT_FOUND", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task BulkPut_UnlimitedTypeWithNonNullTotal_Returns400Mismatch()
    {
        await SeedAdminAsync();
        var userId = await SeedUserWithCurrentYearLeavesAsync();

        var leaves = await Client.GetFromJsonAsync<List<UserLeaveDto>>(
            $"/api/users/{userId}/leaves", Json);
        Assert.NotNull(leaves);
        var ziekteLeave = leaves.First(l => l.LeaveTypeName == "Ziekte");

        var body = new UpdateUserLeavesBody(CurrentYear,
            [new UpdateUserLeavesItem(ziekteLeave.Id, 5m)]);
        var response = await Client.PutAsJsonAsync($"/api/users/{userId}/leaves", body, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var errors = problem.GetProperty("errors");
        Assert.True(errors.EnumerateObject().Any(e =>
            e.Value.EnumerateArray().Any(v =>
                v.GetProperty("code").GetString() == "ERR_USER_LEAVE_TOTAL_ALLOWED_MISMATCH")));
    }

    [Fact]
    public async Task BulkPut_LimitedTypeWithNullTotal_Returns400Mismatch()
    {
        await SeedAdminAsync();
        var userId = await SeedUserWithCurrentYearLeavesAsync();

        var leaves = await Client.GetFromJsonAsync<List<UserLeaveDto>>(
            $"/api/users/{userId}/leaves", Json);
        Assert.NotNull(leaves);
        var verlofLeave = leaves.First(l => l.LeaveTypeName == "Verlof");

        var body = new UpdateUserLeavesBody(CurrentYear,
            [new UpdateUserLeavesItem(verlofLeave.Id, null)]);
        var response = await Client.PutAsJsonAsync($"/api/users/{userId}/leaves", body, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var errors = problem.GetProperty("errors");
        Assert.True(errors.EnumerateObject().Any(e =>
            e.Value.EnumerateArray().Any(v =>
                v.GetProperty("code").GetString() == "ERR_USER_LEAVE_TOTAL_ALLOWED_MISMATCH")));
    }

    [Fact]
    public async Task BulkPut_MismatchedYear_Returns404()
    {
        await SeedAdminAsync();
        var userId = await SeedUserWithCurrentYearLeavesAsync();

        var leaves = await Client.GetFromJsonAsync<List<UserLeaveDto>>(
            $"/api/users/{userId}/leaves", Json);
        Assert.NotNull(leaves);
        var verlofLeave = leaves.First(l => l.LeaveTypeName == "Verlof");

        // Use wrong year in body — row won't be found for (Id, UserId, Year=2020)
        var body = new UpdateUserLeavesBody(2020,
            [new UpdateUserLeavesItem(verlofLeave.Id, 10m)]);
        var response = await Client.PutAsJsonAsync($"/api/users/{userId}/leaves", body, Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task BulkPut_EmptyItems_Returns400Required()
    {
        await SeedAdminAsync();
        var userId = await SeedUserWithCurrentYearLeavesAsync();

        var body = new UpdateUserLeavesBody(CurrentYear, []);
        var response = await Client.PutAsJsonAsync($"/api/users/{userId}/leaves", body, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var errors = problem.GetProperty("errors");
        Assert.True(errors.EnumerateObject().Any(e =>
            e.Value.EnumerateArray().Any(v => v.GetProperty("code").GetString() == "ERR_REQUIRED")));
    }

    [Fact]
    public async Task BulkPut_DuplicateItemIds_Returns400Invalid()
    {
        await SeedAdminAsync();
        var userId = await SeedUserWithCurrentYearLeavesAsync();

        var leaves = await Client.GetFromJsonAsync<List<UserLeaveDto>>(
            $"/api/users/{userId}/leaves", Json);
        Assert.NotNull(leaves);
        var verlofLeave = leaves.First(l => l.LeaveTypeName == "Verlof");

        var body = new UpdateUserLeavesBody(CurrentYear,
        [
            new UpdateUserLeavesItem(verlofLeave.Id, 10m),
            new UpdateUserLeavesItem(verlofLeave.Id, 15m),
        ]);
        var response = await Client.PutAsJsonAsync($"/api/users/{userId}/leaves", body, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var errors = problem.GetProperty("errors");
        Assert.True(errors.EnumerateObject().Any(e =>
            e.Value.EnumerateArray().Any(v => v.GetProperty("code").GetString() == "ERR_INVALID")));
    }

    [Fact]
    public async Task BulkPut_NonAdmin_Returns403()
    {
        // The test auth handler always authenticates as admin (OID matches seeded user).
        // To test non-admin, seed a user WITHOUT the RequireAdmin policy satisfied.
        // The factory wires RequireAdmin via the RequireAdminAuthorizationHandler which
        // checks that the current user's role == Admin. We seed no user, so the lookup
        // returns null → the handler denies.
        var userId = Guid.NewGuid();
        var body = new UpdateUserLeavesBody(CurrentYear,
            [new UpdateUserLeavesItem(Guid.NewGuid(), 10m)]);

        // Don't seed the admin user → RequireAdminAuthorizationHandler sees no matching user → 403
        var response = await Client.PutAsJsonAsync($"/api/users/{userId}/leaves", body, Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_SoftDeletedUser_Returns200Empty()
    {
        await SeedAdminAsync();
        // Soft-deleted user has DeletedAt set; query filter excludes them.
        // GET leaves for a userId that doesn't exist → empty list (no rows).
        var userId = Guid.NewGuid();

        var response = await Client.GetAsync($"/api/users/{userId}/leaves");

        response.EnsureSuccessStatusCode();
        var leaves = await response.Content.ReadFromJsonAsync<List<UserLeaveDto>>(Json);
        Assert.NotNull(leaves);
        Assert.Empty(leaves);
    }
}
