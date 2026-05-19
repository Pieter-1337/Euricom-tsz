using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tsz.Api.Modules.Customers;
using Tsz.Api.Modules.Customers.Features;
using Tsz.Api.Modules.Users;
using Tsz.Api.Tests.Integration.TestAuth;
using Tsz.Infrastructure.Common.Pagination;

namespace Tsz.Api.Tests.Integration;

public class CustomerEndpointsTests : IntegrationTestBase, IAsyncLifetime
{
    private const string TestEmail = "admin@test.com";
    private const string TestOid = "test-user-id";

    public CustomerEndpointsTests(TestWebApplicationFactory factory) : base(factory) { }

    public async Task InitializeAsync()
    {
        await WithUowAsync(uow => uow.RepositoryFor<Customer>().BatchHardDeleteAsync(_ => true));
        await WithUowAsync(uow => uow.RepositoryFor<User>().BatchHardDeleteAsync(_ => true));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private Task SeedAdminAsync() => WithUowAsync(async uow =>
    {
        var user = User.Create("Admin", "Test", TestEmail, [UserRole.Admin]);
        user.LinkEntraOid(TestOid);
        uow.RepositoryFor<User>().Add(user);
        await uow.SaveChangesAsync();
    });

    private Task SeedNonAdminAsync() => WithUowAsync(async uow =>
    {
        var user = User.Create("User", "Test", TestEmail, [UserRole.User]);
        user.LinkEntraOid(TestOid);
        uow.RepositoryFor<User>().Add(user);
        await uow.SaveChangesAsync();
    });

    [Fact]
    public async Task GetCustomers_AsNonAdmin_ReturnsForbidden()
    {
        await SeedNonAdminAsync();

        var response = await Client.GetAsync("/api/customers");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateCustomer_AsAdmin_Valid_ReturnsCreated_AndAssignsNumber()
    {
        await SeedAdminAsync();
        var command = new CreateCustomerCommand(
            "Acme",
            new ContactPersonDto("Jane", "jane@example.com"),
            new AddressDto("Main 1", "1000", "Brussels", "BE"),
            ClientManagerId: null);

        var response = await Client.PostAsJsonAsync("/api/customers", command);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<CustomerDto>(Json);
        Assert.NotNull(dto);
        Assert.Equal(1, dto.Number);
        Assert.Equal("Acme", dto.Name);
        Assert.Equal("jane@example.com", dto.ContactPerson.Email);
        Assert.Equal("Brussels", dto.Address.City);
        Assert.Null(dto.ClientManagerId);
    }

    [Fact]
    public async Task CreateCustomer_Twice_AutoIncrementsNumber()
    {
        await SeedAdminAsync();
        var first = await Client.PostAsJsonAsync("/api/customers",
            new CreateCustomerCommand("A", new ContactPersonDto(null, "a@x.com"), null, ClientManagerId: null));
        var firstDto = (await first.Content.ReadFromJsonAsync<CustomerDto>(Json))!;
        Assert.Equal(1, firstDto.Number);

        var second = await Client.PostAsJsonAsync("/api/customers",
            new CreateCustomerCommand("B", new ContactPersonDto(null, "b@x.com"), null, ClientManagerId: null));
        var secondDto = (await second.Content.ReadFromJsonAsync<CustomerDto>(Json))!;
        Assert.Equal(2, secondDto.Number);
    }

    [Fact]
    public async Task CreateCustomer_InvalidEmail_Returns400()
    {
        await SeedAdminAsync();
        var command = new CreateCustomerCommand("Acme", new ContactPersonDto(null, "not-an-email"), null, ClientManagerId: null);

        var response = await Client.PostAsJsonAsync("/api/customers", command);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetCustomerById_Missing_Returns404()
    {
        await SeedAdminAsync();

        var response = await Client.GetAsync($"/api/customers/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCustomer_Existing_ReturnsOk_AndPreservesNumber()
    {
        await SeedAdminAsync();
        var created = await Client.PostAsJsonAsync("/api/customers",
            new CreateCustomerCommand("Old", new ContactPersonDto(null, "old@x.com"), null, ClientManagerId: null));
        var dto = (await created.Content.ReadFromJsonAsync<CustomerDto>(Json))!;

        var update = new UpdateCustomerCommand(
            dto.Id,
            "New",
            new ContactPersonDto("Bob", "new@x.com"),
            new AddressDto(null, null, "Antwerp", null),
            ClientManagerId: null);
        var response = await Client.PutAsJsonAsync($"/api/customers/{dto.Id}", update);

        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<CustomerDto>(Json);
        Assert.NotNull(updated);
        Assert.Equal(dto.Number, updated.Number);
        Assert.Equal("New", updated.Name);
        Assert.Equal("Antwerp", updated.Address.City);
        Assert.Equal("Bob", updated.ContactPerson.Name);
    }

    [Fact]
    public async Task UpdateCustomer_RouteIdMismatch_ReturnsBadRequest()
    {
        await SeedAdminAsync();
        var update = new UpdateCustomerCommand(Guid.NewGuid(), "x", new ContactPersonDto(null, "x@x.com"), null, ClientManagerId: null);

        var response = await Client.PutAsJsonAsync($"/api/customers/{Guid.NewGuid()}", update);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCustomer_Missing_Returns404()
    {
        await SeedAdminAsync();
        var id = Guid.NewGuid();

        var response = await Client.PutAsJsonAsync($"/api/customers/{id}",
            new UpdateCustomerCommand(id, "x", new ContactPersonDto(null, "x@x.com"), null, ClientManagerId: null));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("ERR_CUSTOMER_NOT_FOUND", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task DeleteCustomer_Existing_SoftDeletes()
    {
        await SeedAdminAsync();
        var created = await Client.PostAsJsonAsync("/api/customers",
            new CreateCustomerCommand("Doomed", new ContactPersonDto(null, "doomed@x.com"), null, ClientManagerId: null));
        var dto = (await created.Content.ReadFromJsonAsync<CustomerDto>(Json))!;

        var response = await Client.DeleteAsync($"/api/customers/{dto.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var getResponse = await Client.GetAsync($"/api/customers/{dto.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);

        var row = await WithUowAsync(uow =>
            uow.RepositoryFor<Customer>().FirstOrDefaultAsync(c => c.Id == dto.Id, ignoreQueryFilters: true));
        Assert.NotNull(row);
        Assert.NotNull(row.DeletedAt);
    }

    [Fact]
    public async Task GetCustomersPaged_PaginatesAndReturnsTotal()
    {
        await SeedAdminAsync();
        for (var i = 1; i <= 12; i++)
        {
            await Client.PostAsJsonAsync("/api/customers",
                new CreateCustomerCommand($"Cust {i:D2}", new ContactPersonDto(null, $"c{i}@x.com"), null, ClientManagerId: null));
        }

        var firstResponse = await Client.GetAsync("/api/customers/paged?pageSize=10&sortBy=number");
        firstResponse.EnsureSuccessStatusCode();
        var firstPage = await firstResponse.Content.ReadFromJsonAsync<KeysetPage<CustomerDto>>(Json);
        Assert.NotNull(firstPage);
        Assert.Equal(10, firstPage.Items.Count);
        Assert.Equal(12, firstPage.Total);
        Assert.NotNull(firstPage.NextCursor);

        var secondResponse = await Client.GetAsync(
            $"/api/customers/paged?pageSize=10&sortBy=number&cursor={Uri.EscapeDataString(firstPage.NextCursor)}");
        secondResponse.EnsureSuccessStatusCode();
        var secondPage = await secondResponse.Content.ReadFromJsonAsync<KeysetPage<CustomerDto>>(Json);
        Assert.NotNull(secondPage);
        Assert.Equal(2, secondPage.Items.Count);
        Assert.Null(secondPage.NextCursor);
    }

    [Fact]
    public async Task GetCustomersPaged_Search_FiltersByContactEmail()
    {
        await SeedAdminAsync();
        await Client.PostAsJsonAsync("/api/customers",
            new CreateCustomerCommand("Alpha", new ContactPersonDto(null, "alice@example.com"), null, ClientManagerId: null));
        await Client.PostAsJsonAsync("/api/customers",
            new CreateCustomerCommand("Beta", new ContactPersonDto(null, "bob@example.com"), null, ClientManagerId: null));

        var response = await Client.GetAsync("/api/customers/paged?search=alice");
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<KeysetPage<CustomerDto>>(Json);
        Assert.NotNull(page);
        Assert.All(page.Items, c =>
            Assert.True(
                c.Name.Contains("alice", StringComparison.OrdinalIgnoreCase) ||
                c.ContactPerson.Email.Contains("alice", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task GetCustomersPaged_DeletedOnly_ReturnsOnlySoftDeleted()
    {
        await SeedAdminAsync();
        var created = await Client.PostAsJsonAsync("/api/customers",
            new CreateCustomerCommand("Doomed", new ContactPersonDto(null, "doomed@x.com"), null, ClientManagerId: null));
        var dto = (await created.Content.ReadFromJsonAsync<CustomerDto>(Json))!;
        await Client.DeleteAsync($"/api/customers/{dto.Id}");

        var response = await Client.GetAsync("/api/customers/paged?deletedOnly=true");
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<KeysetPage<CustomerDto>>(Json);
        Assert.NotNull(page);
        Assert.Contains(page.Items, c => c.Id == dto.Id);
    }

    [Fact]
    public async Task GetCustomersPaged_AsNonAdmin_ReturnsForbidden()
    {
        await SeedNonAdminAsync();

        var response = await Client.GetAsync("/api/customers/paged");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<Guid> SeedClientManagerAsync(string email = "cm@test.com")
    {
        var id = Guid.NewGuid();
        await WithUowAsync(async uow =>
        {
            var user = User.Create("Cam", "Manager", email, [UserRole.ClientManager]);
            user.Id = id;
            uow.RepositoryFor<User>().Add(user);
            await uow.SaveChangesAsync();
        });
        return id;
    }

    [Fact]
    public async Task CreateCustomer_WithValidClientManager_PersistsAssignment()
    {
        await SeedAdminAsync();
        var managerId = await SeedClientManagerAsync();

        var response = await Client.PostAsJsonAsync("/api/customers",
            new CreateCustomerCommand("Acme", new ContactPersonDto(null, "a@x.com"), null, ClientManagerId: managerId));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<CustomerDto>(Json);
        Assert.NotNull(dto);
        Assert.Equal(managerId, dto.ClientManagerId);
    }

    [Fact]
    public async Task CreateCustomer_WithUserMissingClientManagerRole_Returns400()
    {
        await SeedAdminAsync();
        // SeedAdmin user has Admin role only, not ClientManager
        var adminId = Guid.Empty;
        await WithUowAsync(async uow =>
        {
            var existing = await uow.RepositoryFor<User>().FirstOrDefaultAsync(u => u.Email == TestEmail);
            adminId = existing!.Id;
        });

        var response = await Client.PostAsJsonAsync("/api/customers",
            new CreateCustomerCommand("Acme", new ContactPersonDto(null, "a@x.com"), null, ClientManagerId: adminId));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("ERR_CUSTOMER_CLIENT_MANAGER_MISSING_ROLE", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task CreateCustomer_WithUnknownClientManager_Returns404()
    {
        await SeedAdminAsync();

        var response = await Client.PostAsJsonAsync("/api/customers",
            new CreateCustomerCommand("Acme", new ContactPersonDto(null, "a@x.com"), null, ClientManagerId: Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("ERR_CUSTOMER_CLIENT_MANAGER_NOT_FOUND", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task UpdateCustomer_AssignsAndClearsClientManager()
    {
        await SeedAdminAsync();
        var managerId = await SeedClientManagerAsync();
        var created = await Client.PostAsJsonAsync("/api/customers",
            new CreateCustomerCommand("Acme", new ContactPersonDto(null, "a@x.com"), null, ClientManagerId: null));
        var dto = (await created.Content.ReadFromJsonAsync<CustomerDto>(Json))!;

        var assigned = await Client.PutAsJsonAsync($"/api/customers/{dto.Id}",
            new UpdateCustomerCommand(dto.Id, "Acme", new ContactPersonDto(null, "a@x.com"), null, ClientManagerId: managerId));
        var assignedDto = (await assigned.Content.ReadFromJsonAsync<CustomerDto>(Json))!;
        Assert.Equal(managerId, assignedDto.ClientManagerId);

        var cleared = await Client.PutAsJsonAsync($"/api/customers/{dto.Id}",
            new UpdateCustomerCommand(dto.Id, "Acme", new ContactPersonDto(null, "a@x.com"), null, ClientManagerId: null));
        var clearedDto = (await cleared.Content.ReadFromJsonAsync<CustomerDto>(Json))!;
        Assert.Null(clearedDto.ClientManagerId);
    }
}
