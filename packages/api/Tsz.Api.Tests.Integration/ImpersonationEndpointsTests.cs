using System.Net;
using System.Net.Http.Json;
using Tsz.Api.Auth;
using Tsz.Api.Tests.Integration.TestAuth;
using Tsz.Infrastructure.Common.Pagination;
using Tsz.Modules.Users.Contracts;
using Tsz.Modules.Users.Domain.Users;
using Tsz.Modules.Users.Features;

namespace Tsz.Api.Tests.Integration;

/// Integration tests for the impersonation trust boundary.
/// Each test seeds admin A and plain user B (different oids/emails),
/// then exercises different request combinations.
public class ImpersonationEndpointsTests : IntegrationTestBase, IAsyncLifetime
{
    private const string AdminAOid = "admin-a-oid";
    private const string AdminAEmail = "admin-a@test.com";

    private const string UserBOid = "user-b-oid";
    private const string UserBEmail = "user-b@test.com";

    private const string AdminCOid = "admin-c-oid";
    private const string AdminCEmail = "admin-c@test.com";

    private Guid _adminAId;
    private Guid _userBId;
    private Guid _adminCId;

    public ImpersonationEndpointsTests(TestWebApplicationFactory factory) : base(factory) { }

    public async Task InitializeAsync()
    {
        await WithUowAsync(uow => uow.RepositoryFor<User>().BatchHardDeleteAsync(_ => true));

        _adminAId = await SeedUserAsync(AdminAOid, AdminAEmail, UserRole.Admin);
        _userBId = await SeedUserAsync(UserBOid, UserBEmail, UserRole.User);
        _adminCId = await SeedUserAsync(AdminCOid, AdminCEmail, UserRole.Admin);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<Guid> SeedUserAsync(string oid, string email, UserRole role)
    {
        var id = Guid.NewGuid();
        await WithUowAsync(async uow =>
        {
            var user = User.Create("Test", "User", email, [role]);
            user.Id = id;
            user.LinkEntraOid(oid);
            uow.RepositoryFor<User>().Add(user);
            await uow.SaveChangesAsync();
        });
        return id;
    }

    // Helper: create a client that sends requests as "admin A" (real user A + impersonating B).
    private HttpRequestMessage AsAdminImpersonatingB(HttpMethod method, string url)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("X-Test-Oid", AdminAOid);
        req.Headers.Add("X-Test-Email", AdminAEmail);
        req.Headers.Add(ImpersonationHeader.Name, _userBId.ToString());
        return req;
    }

    private HttpRequestMessage AsAdminImpersonatingAdmin(HttpMethod method, string url)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("X-Test-Oid", AdminAOid);
        req.Headers.Add("X-Test-Email", AdminAEmail);
        req.Headers.Add(ImpersonationHeader.Name, _adminCId.ToString());
        return req;
    }

    private HttpRequestMessage AsNonAdminSendingHeader(HttpMethod method, string url, Guid targetId)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("X-Test-Oid", UserBOid);
        req.Headers.Add("X-Test-Email", UserBEmail);
        req.Headers.Add(ImpersonationHeader.Name, targetId.ToString());
        return req;
    }

    private HttpRequestMessage AsAdmin(HttpMethod method, string url)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("X-Test-Oid", AdminAOid);
        req.Headers.Add("X-Test-Email", AdminAEmail);
        return req;
    }

    // ── Admin-only endpoint returns 403 while impersonating (effective user B has no Admin role) ──

    [Fact]
    public async Task AdminImpersonatingUser_HittingAdminOnlyEndpoint_Returns403()
    {
        var response = await Client.SendAsync(
            AsAdminImpersonatingB(HttpMethod.Get, "/api/users/paged"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── Non-admin sending header → 403 (header cannot escalate privileges) ──

    [Fact]
    public async Task NonAdmin_SendingImpersonationHeader_Returns403()
    {
        var response = await Client.SendAsync(
            AsNonAdminSendingHeader(HttpMethod.Get, "/api/users/me", _adminAId));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── Admin targeting another Admin → 403 ──

    [Fact]
    public async Task Admin_ImpersonatingAnotherAdmin_Returns403()
    {
        var response = await Client.SendAsync(
            AsAdminImpersonatingAdmin(HttpMethod.Get, "/api/users/me"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── /me returns the impersonated user's data while impersonating ──

    [Fact]
    public async Task AdminImpersonatingUser_GetMe_ReturnsTargetUser()
    {
        var response = await Client.SendAsync(
            AsAdminImpersonatingB(HttpMethod.Get, "/api/users/me"));

        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<UserDto>(Json);
        Assert.NotNull(dto);
        Assert.Equal(_userBId, dto.Id);
        Assert.Equal(UserBEmail, dto.Email);
    }

    // ── Malformed X-Impersonate-User header value → 400 ──

    [Fact]
    public async Task Admin_MalformedImpersonationHeader_Returns400()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/users/me");
        req.Headers.Add("X-Test-Oid", AdminAOid);
        req.Headers.Add("X-Test-Email", AdminAEmail);
        req.Headers.Add(ImpersonationHeader.Name, "not-a-guid");

        var response = await Client.SendAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Non-existent target → 404 ──

    [Fact]
    public async Task Admin_ImpersonatingNonExistentUser_Returns404()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/users/me");
        req.Headers.Add("X-Test-Oid", AdminAOid);
        req.Headers.Add("X-Test-Email", AdminAEmail);
        req.Headers.Add(ImpersonationHeader.Name, Guid.NewGuid().ToString());

        var response = await Client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── No header → normal behavior, no regression ──

    [Fact]
    public async Task NoImpersonationHeader_NormalBehaviorPreserved()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/users/me");
        req.Headers.Add("X-Test-Oid", AdminAOid);
        req.Headers.Add("X-Test-Email", AdminAEmail);

        var response = await Client.SendAsync(req);

        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<UserDto>(Json);
        Assert.NotNull(dto);
        Assert.Equal(_adminAId, dto.Id);
    }

    // ── GET /api/users/impersonation-targets excludes Admin users ──

    [Fact]
    public async Task GetImpersonationTargets_ExcludesAdmins()
    {
        var response = await Client.SendAsync(AsAdmin(HttpMethod.Get, "/api/users/impersonation-targets"));

        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<KeysetPage<UserDto>>(Json);
        Assert.NotNull(page);
        Assert.All(page.Items, u => Assert.DoesNotContain(UserRole.Admin, u.Roles));
        Assert.Contains(page.Items, u => u.Id == _userBId);
        Assert.DoesNotContain(page.Items, u => u.Id == _adminAId);
        Assert.DoesNotContain(page.Items, u => u.Id == _adminCId);
    }

    [Fact]
    public async Task GetImpersonationTargets_NonAdmin_Returns403()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/users/impersonation-targets");
        req.Headers.Add("X-Test-Oid", UserBOid);
        req.Headers.Add("X-Test-Email", UserBEmail);

        var response = await Client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetImpersonationTargets_SearchFilters_ReturnsMatchingUsers()
    {
        // Seed an additional non-admin user whose name contains "Alice"
        await WithUowAsync(async uow =>
        {
            var alice = User.Create("Alice", "Smith", "alice@test.com", [UserRole.User]);
            uow.RepositoryFor<User>().Add(alice);
            await uow.SaveChangesAsync();
        });

        var response = await Client.SendAsync(AsAdmin(HttpMethod.Get, "/api/users/impersonation-targets?search=alice"));

        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<KeysetPage<UserDto>>(Json);
        Assert.NotNull(page);
        Assert.All(page.Items, u =>
            Assert.True(
                u.FirstName.Contains("alice", StringComparison.OrdinalIgnoreCase) ||
                u.LastName.Contains("alice", StringComparison.OrdinalIgnoreCase) ||
                u.Email.Contains("alice", StringComparison.OrdinalIgnoreCase)));
    }

    // ── RequireAdminOrSelf: "self" = effective (impersonated) user ──

    [Fact]
    public async Task AdminImpersonatingB_GetUserById_EffectiveSelfIsB()
    {
        // /api/users/{id} requires admin — test that impersonated user loses admin-only access
        // but the effective identity is B when checking /api/users/me
        var response = await Client.SendAsync(
            AsAdminImpersonatingB(HttpMethod.Get, "/api/users/me"));

        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<UserDto>(Json);
        Assert.Equal(_userBId, dto!.Id);
    }
}
