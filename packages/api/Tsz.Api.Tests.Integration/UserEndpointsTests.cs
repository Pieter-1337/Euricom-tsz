using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tsz.Modules.Customers.Domain.Customers;
using Tsz.Modules.Customers.Features;
using Tsz.Modules.LeaveTypes.Domain.Leaves;
using Tsz.Modules.Users.Contracts;
using Tsz.Modules.Users.Domain.Users;
using Tsz.Modules.Users.Features;
using Tsz.Api.Tests.Integration.TestAuth;
using Tsz.Infrastructure.Common.Pagination;

namespace Tsz.Api.Tests.Integration;

public class UserEndpointsTests : IntegrationTestBase, IAsyncLifetime
{
    private const string TestEmail = "test@test.com";
    private const string TestOid = "test-user-id";

    public UserEndpointsTests(TestWebApplicationFactory factory) : base(factory) { }

    public async Task InitializeAsync()
    {
        await WithUowAsync(uow => uow.RepositoryFor<Customer>().BatchHardDeleteAsync(_ => true));
        await WithUowAsync(uow => uow.RepositoryFor<User>().BatchHardDeleteAsync(_ => true));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private Task SeedUserAsync(string email, UserRole role, string? oid = null) => WithUowAsync(async uow =>
    {
        var user = User.Create("User", $"_{Guid.NewGuid().ToString()[..6]}", email, [role]);
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
    public async Task GetUsers_AsClientManager_ReturnsOk()
    {
        await SeedUserAsync(TestEmail, UserRole.ClientManager, oid: TestOid);

        var response = await Client.GetAsync("/api/users");

        response.EnsureSuccessStatusCode();
        var list = await response.Content.ReadFromJsonAsync<List<UserDto>>(Json);
        Assert.NotNull(list);
        Assert.Contains(list, u => u.Email == TestEmail);
    }

    [Fact]
    public async Task GetUsersPaged_AsClientManager_ReturnsForbidden()
    {
        await SeedUserAsync(TestEmail, UserRole.ClientManager, oid: TestOid);

        var response = await Client.GetAsync("/api/users/paged");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateUser_AsAdmin_Valid_ReturnsCreated()
    {
        await SeedUserAsync(TestEmail, UserRole.Admin, oid: TestOid);
        var command = new CreateUserCommand("Jane", "Doe", "jane@example.com", [UserRole.User]);

        var response = await Client.PostAsJsonAsync("/api/users", command);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<UserDto>(Json);
        Assert.NotNull(dto);
        Assert.Equal("Jane", dto.FirstName);
        Assert.Equal("Doe", dto.LastName);
        Assert.Equal("jane@example.com", dto.Email);
        Assert.Equal(new[] { UserRole.User }, dto.Roles);
    }

    [Fact]
    public async Task CreateUser_WithMultipleRoles_PersistsAllRoles()
    {
        await SeedUserAsync(TestEmail, UserRole.Admin, oid: TestOid);
        var command = new CreateUserCommand("Multi", "Role", "multi@example.com", [UserRole.Admin, UserRole.ClientManager]);

        var response = await Client.PostAsJsonAsync("/api/users", command);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<UserDto>(Json);
        Assert.NotNull(dto);
        Assert.Equal(2, dto.Roles.Count);
        Assert.Contains(UserRole.Admin, dto.Roles);
        Assert.Contains(UserRole.ClientManager, dto.Roles);
    }

    [Fact]
    public async Task UpdateUser_RemovingAdminRole_RevokesAdminAccess()
    {
        await SeedUserAsync(TestEmail, UserRole.Admin, oid: TestOid);

        var meBefore = await Client.GetAsync("/api/users");
        meBefore.EnsureSuccessStatusCode();

        var current = await GetUserByEmailAsync(TestEmail);
        Assert.NotNull(current);
        var demote = new UpdateUserCommand(current.Id, current.FirstName, current.LastName, [UserRole.User]);
        var put = await Client.PutAsJsonAsync($"/api/users/{current.Id}", demote);
        put.EnsureSuccessStatusCode();

        var meAfter = await Client.GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.Forbidden, meAfter.StatusCode);
    }

    [Fact]
    public async Task CreateUser_EmptyRoles_ReturnsBadRequest()
    {
        await SeedUserAsync(TestEmail, UserRole.Admin, oid: TestOid);
        var command = new CreateUserCommand("Jane", "Doe", "noroles@example.com", []);

        var response = await Client.PostAsJsonAsync("/api/users", command);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetUsers_AsAdminAmongManyRoles_ReturnsOk()
    {
        await WithUowAsync(async uow =>
        {
            var user = User.Create("Multi", "Admin", TestEmail, [UserRole.ClientManager, UserRole.Admin]);
            user.LinkEntraOid(TestOid);
            uow.RepositoryFor<User>().Add(user);
            await uow.SaveChangesAsync();
        });

        var response = await Client.GetAsync("/api/users");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task CreateUser_AsAdmin_SeedsUserLeaveRows()
    {
        await SeedUserAsync(TestEmail, UserRole.Admin, oid: TestOid);
        var command = new CreateUserCommand("Jane", "Doe", "jane@example.com", [UserRole.User]);

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
        var command = new CreateUserCommand("", "", "not-an-email", [UserRole.User]);

        var response = await Client.PostAsJsonAsync("/api/users", command);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateUser_DuplicateEmail_Returns409WithProblemDetails()
    {
        await SeedUserAsync(TestEmail, UserRole.Admin, oid: TestOid);
        var command = new CreateUserCommand("Jane", "Doe", "dup@example.com", [UserRole.User]);
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
        var command = new CreateUserCommand("Jane", "Doe", "not-an-email", [UserRole.User]);

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
            new CreateUserCommand("Old", "Name", "u@example.com", [UserRole.User]));
        var dto = (await created.Content.ReadFromJsonAsync<UserDto>(Json))!;

        var update = new UpdateUserCommand(dto.Id, "New", "Name", [UserRole.Admin, UserRole.ClientManager]);
        var response = await Client.PutAsJsonAsync($"/api/users/{dto.Id}", update);

        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<UserDto>(Json);
        Assert.NotNull(updated);
        Assert.Equal("New", updated.FirstName);
        Assert.Equal(new[] { UserRole.Admin, UserRole.ClientManager }, updated.Roles);
    }

    [Fact]
    public async Task UpdateUser_RouteIdMismatch_ReturnsBadRequest()
    {
        await SeedUserAsync(TestEmail, UserRole.Admin, oid: TestOid);
        var update = new UpdateUserCommand(Guid.NewGuid(), "x", "y", [UserRole.User]);

        var response = await Client.PutAsJsonAsync($"/api/users/{Guid.NewGuid()}", update);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateUser_Missing_Returns404WithProblemDetails()
    {
        await SeedUserAsync(TestEmail, UserRole.Admin, oid: TestOid);
        var id = Guid.NewGuid();

        var response = await Client.PutAsJsonAsync($"/api/users/{id}",
            new UpdateUserCommand(id, "x", "y", [UserRole.User]));

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
            new CreateUserCommand("Doomed", "User", "doomed@example.com", [UserRole.User]));
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

    // ── GET /api/users/paged ──────────────────────────────────────────────────


    [Fact]
    public async Task GetUsersPaged_AsNonAdmin_ReturnsForbidden()
    {
        await SeedUserAsync(TestEmail, UserRole.User, oid: TestOid);

        var response = await Client.GetAsync("/api/users/paged");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetUsersPaged_PaginatesThrough23Users()
    {
        await SeedUserAsync(TestEmail, UserRole.Admin, oid: TestOid);
        for (var i = 1; i <= 22; i++)
            await SeedUserAsync($"user{i:D2}@example.com", UserRole.User);

        var firstResponse = await Client.GetAsync("/api/users/paged?pageSize=10");
        firstResponse.EnsureSuccessStatusCode();
        var firstPage = await firstResponse.Content.ReadFromJsonAsync<KeysetPage<UserDto>>(Json);
        Assert.NotNull(firstPage);
        Assert.Equal(10, firstPage.Items.Count);
        Assert.Equal(23, firstPage.Total);
        Assert.NotNull(firstPage.NextCursor);

        var secondResponse = await Client.GetAsync($"/api/users/paged?pageSize=10&cursor={Uri.EscapeDataString(firstPage.NextCursor)}");
        secondResponse.EnsureSuccessStatusCode();
        var secondPage = await secondResponse.Content.ReadFromJsonAsync<KeysetPage<UserDto>>(Json);
        Assert.NotNull(secondPage);
        Assert.Equal(10, secondPage.Items.Count);
        Assert.Equal(23, secondPage.Total);
        Assert.NotNull(secondPage.NextCursor);

        var thirdResponse = await Client.GetAsync($"/api/users/paged?pageSize=10&cursor={Uri.EscapeDataString(secondPage.NextCursor)}");
        thirdResponse.EnsureSuccessStatusCode();
        var thirdPage = await thirdResponse.Content.ReadFromJsonAsync<KeysetPage<UserDto>>(Json);
        Assert.NotNull(thirdPage);
        Assert.Equal(3, thirdPage.Items.Count);
        Assert.Equal(23, thirdPage.Total);
        Assert.Null(thirdPage.NextCursor);

        // All item IDs distinct across the three pages.
        var allIds = firstPage.Items.Select(u => u.Id)
            .Concat(secondPage.Items.Select(u => u.Id))
            .Concat(thirdPage.Items.Select(u => u.Id))
            .ToList();
        Assert.Equal(23, allIds.Distinct().Count());
    }

    [Fact]
    public async Task GetUsersPaged_Search_FiltersResults()
    {
        await SeedUserAsync(TestEmail, UserRole.Admin, oid: TestOid);
        await WithUowAsync(async uow =>
        {
            uow.RepositoryFor<User>().Add(User.Create("Alice", "Smith", "alice@example.com", [UserRole.User]));
            uow.RepositoryFor<User>().Add(User.Create("Bob", "Jones", "bob@example.com", [UserRole.User]));
            await uow.SaveChangesAsync();
        });

        var response = await Client.GetAsync("/api/users/paged?search=alice");
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<KeysetPage<UserDto>>(Json);
        Assert.NotNull(page);
        Assert.All(page.Items, u =>
            Assert.True(
                u.FirstName.Contains("alice", StringComparison.OrdinalIgnoreCase) ||
                u.LastName.Contains("alice", StringComparison.OrdinalIgnoreCase) ||
                u.Email.Contains("alice", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task GetUsersPaged_SortByEmailDesc_ReturnsCorrectOrder()
    {
        await SeedUserAsync(TestEmail, UserRole.Admin, oid: TestOid);
        await WithUowAsync(async uow =>
        {
            uow.RepositoryFor<User>().Add(User.Create("A", "A", "aaa@example.com", [UserRole.User]));
            uow.RepositoryFor<User>().Add(User.Create("Z", "Z", "zzz@example.com", [UserRole.User]));
            await uow.SaveChangesAsync();
        });

        var response = await Client.GetAsync("/api/users/paged?sortBy=email&sortDir=Desc");
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<KeysetPage<UserDto>>(Json);
        Assert.NotNull(page);
        Assert.True(page.Items.Count >= 2);

        for (var i = 0; i < page.Items.Count - 1; i++)
            Assert.True(
                string.Compare(page.Items[i].Email, page.Items[i + 1].Email, StringComparison.OrdinalIgnoreCase) >= 0,
                $"Expected descending order but found {page.Items[i].Email} before {page.Items[i + 1].Email}");
    }

    [Fact]
    public async Task GetUsersPaged_DeletedOnly_ReturnsOnlySoftDeletedUsers()
    {
        await SeedUserAsync(TestEmail, UserRole.Admin, oid: TestOid);
        var createResponse = await Client.PostAsJsonAsync("/api/users",
            new CreateUserCommand("Doomed", "User", "doomed2@example.com", [UserRole.User]));
        var dto = (await createResponse.Content.ReadFromJsonAsync<UserDto>(Json))!;
        await Client.DeleteAsync($"/api/users/{dto.Id}");

        var deletedOnly = await Client.GetAsync("/api/users/paged?deletedOnly=true");
        deletedOnly.EnsureSuccessStatusCode();
        var page = await deletedOnly.Content.ReadFromJsonAsync<KeysetPage<UserDto>>(Json);
        Assert.NotNull(page);
        Assert.Contains(page.Items, u => u.Id == dto.Id);
        Assert.DoesNotContain(page.Items, u => u.Email == TestEmail);
    }

    [Fact]
    public async Task GetUsersPaged_BadSortBy_Returns400()
    {
        await SeedUserAsync(TestEmail, UserRole.Admin, oid: TestOid);

        var response = await Client.GetAsync("/api/users/paged?sortBy=ssn");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetUsersPaged_BadCursor_Returns400()
    {
        await SeedUserAsync(TestEmail, UserRole.Admin, oid: TestOid);

        var response = await Client.GetAsync("/api/users/paged?cursor=garbage");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── ?role= filter + ClientManager guards ─────────────────────────────────

    [Fact]
    public async Task GetUsers_WithRoleFilter_ReturnsOnlyMatchingUsers()
    {
        await SeedUserAsync(TestEmail, UserRole.Admin, oid: TestOid);
        await WithUowAsync(async uow =>
        {
            uow.RepositoryFor<User>().Add(User.Create("Cam", "Manager", "cm@example.com", [UserRole.ClientManager]));
            uow.RepositoryFor<User>().Add(User.Create("Reg", "User", "reg@example.com", [UserRole.User]));
            await uow.SaveChangesAsync();
        });

        var response = await Client.GetAsync("/api/users?role=ClientManager");
        response.EnsureSuccessStatusCode();
        var list = await response.Content.ReadFromJsonAsync<List<UserDto>>(Json);
        Assert.NotNull(list);
        Assert.All(list, u => Assert.Contains(UserRole.ClientManager, u.Roles));
        Assert.Contains(list, u => u.Email == "cm@example.com");
        Assert.DoesNotContain(list, u => u.Email == "reg@example.com");
    }

    [Fact]
    public async Task UpdateUser_RemovingClientManagerRole_WhileLinkedToCustomer_Returns409()
    {
        await SeedUserAsync(TestEmail, UserRole.Admin, oid: TestOid);
        var managerId = await SeedManagerAndLinkCustomerAsync();

        var update = new UpdateUserCommand(managerId, "Cam", "Manager", [UserRole.User]);
        var response = await Client.PutAsJsonAsync($"/api/users/{managerId}", update);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("ERR_USER_CANNOT_REMOVE_CLIENT_MANAGER_ROLE_WHILE_ASSIGNED",
            body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task UpdateUser_RemovingClientManagerRole_AfterCustomerSoftDeleted_Succeeds()
    {
        await SeedUserAsync(TestEmail, UserRole.Admin, oid: TestOid);
        var managerId = await SeedManagerAndLinkCustomerAsync();
        await WithUowAsync(async uow =>
        {
            var custRepo = uow.RepositoryFor<Customer>();
            var customer = (await custRepo.GetAllAsListAsync()).First();
            customer.SoftDelete(DateTimeOffset.UtcNow);
            await uow.SaveChangesAsync();
        });

        var update = new UpdateUserCommand(managerId, "Cam", "Manager", [UserRole.User]);
        var response = await Client.PutAsJsonAsync($"/api/users/{managerId}", update);

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task DeleteUser_LinkedAsClientManager_Returns409()
    {
        await SeedUserAsync(TestEmail, UserRole.Admin, oid: TestOid);
        var managerId = await SeedManagerAndLinkCustomerAsync();

        var response = await Client.DeleteAsync($"/api/users/{managerId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("ERR_USER_CANNOT_REMOVE_CLIENT_MANAGER_ROLE_WHILE_ASSIGNED",
            body.GetProperty("code").GetString());
    }

    private async Task<Guid> SeedManagerAndLinkCustomerAsync()
    {
        var managerId = Guid.NewGuid();
        await WithUowAsync(async uow =>
        {
            var manager = User.Create("Cam", "Manager", "cm@example.com", [UserRole.ClientManager]);
            manager.Id = managerId;
            uow.RepositoryFor<User>().Add(manager);

            var customer = Customer.Create(
                1,
                "Acme",
                ContactPerson.Create(null, "a@x.com"),
                clientManagerId: managerId);
            uow.RepositoryFor<Customer>().Add(customer);

            await uow.SaveChangesAsync();
        });
        return managerId;
    }
}
