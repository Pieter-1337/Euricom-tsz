using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tsz.Api.Tests.Integration.TestAuth;
using Tsz.Infrastructure.Common.Pagination;
using Tsz.Modules.Contracts.Domain.Contracts;
using Tsz.Modules.Contracts.Features;
using Tsz.Modules.Customers.Domain.Customers;
using Tsz.Modules.Users.Contracts;
using Tsz.Modules.Users.Domain.Users;

namespace Tsz.Api.Tests.Integration;

public class ContractEndpointsTests : IntegrationTestBase, IAsyncLifetime
{
    private const string TestEmail = "admin@test.com";
    private const string TestOid = "test-user-id";

    public ContractEndpointsTests(TestWebApplicationFactory factory) : base(factory) { }

    public async Task InitializeAsync()
    {
        await WithUowAsync(uow => uow.RepositoryFor<Contract>().BatchHardDeleteAsync(_ => true));
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

    private async Task<Guid> SeedCustomerAsync(Guid? clientManagerId = null)
    {
        var id = Guid.NewGuid();
        await WithUowAsync(async uow =>
        {
            var customer = Customer.Create(1, "Acme", ContactPerson.Create(null, "a@x.com"),
                clientManagerId: clientManagerId);
            customer.Id = id;
            uow.RepositoryFor<Customer>().Add(customer);
            await uow.SaveChangesAsync();
        });
        return id;
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

    private async Task<Guid> SeedClientManagerAsCallerAsync(string email = "caller-cm@test.com")
    {
        var id = Guid.NewGuid();
        await WithUowAsync(async uow =>
        {
            var user = User.Create("Caller", "Manager", email, [UserRole.ClientManager]);
            user.Id = id;
            user.LinkEntraOid(TestOid);
            uow.RepositoryFor<User>().Add(user);
            await uow.SaveChangesAsync();
        });
        return id;
    }

    [Fact]
    public async Task CreateContract_AsAdmin_Valid_ReturnsCreated_AndAssignsNumber()
    {
        await SeedAdminAsync();
        var managerId = await SeedClientManagerAsync();
        var customerId = await SeedCustomerAsync(clientManagerId: managerId);

        var command = new CreateContractCommand(
            Subject: "Engagement Alpha",
            CustomerId: customerId,
            Start: new DateOnly(2026, 1, 1),
            End: new DateOnly(2026, 12, 31));

        var response = await Client.PostAsJsonAsync("/api/contracts", command);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<ContractDto>(Json);
        Assert.NotNull(dto);
        Assert.Equal(1, dto.Number);
        Assert.Equal("Engagement Alpha", dto.Subject);
        Assert.Equal(customerId, dto.CustomerId);
        Assert.Equal(managerId, dto.ClientManagerId);
        Assert.Equal(new DateOnly(2026, 1, 1), dto.Start);
        Assert.Equal(new DateOnly(2026, 12, 31), dto.End);
    }

    [Fact]
    public async Task CreateContract_CustomerWithoutManager_ContractManagerIsNull()
    {
        await SeedAdminAsync();
        var customerId = await SeedCustomerAsync();

        var command = new CreateContractCommand(
            Subject: "Engagement Solo",
            CustomerId: customerId,
            Start: new DateOnly(2026, 1, 1),
            End: null);

        var response = await Client.PostAsJsonAsync("/api/contracts", command);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<ContractDto>(Json);
        Assert.NotNull(dto);
        Assert.Null(dto.ClientManagerId);
    }

    [Fact]
    public async Task CreateContract_AsNonAdmin_ReturnsForbidden()
    {
        await SeedNonAdminAsync();

        var command = new CreateContractCommand(
            Subject: "Engagement",
            CustomerId: Guid.NewGuid(),
            Start: new DateOnly(2026, 1, 1),
            End: null);

        var response = await Client.PostAsJsonAsync("/api/contracts", command);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteContract_AsAdmin_Existing_SoftDeletes()
    {
        await SeedAdminAsync();
        var customerId = await SeedCustomerAsync();
        var managerId = await SeedClientManagerAsync();

        var created = await Client.PostAsJsonAsync("/api/contracts", new CreateContractCommand(
            Subject: "Doomed",
            CustomerId: customerId,
            Start: new DateOnly(2026, 1, 1),
            End: null));
        var dto = (await created.Content.ReadFromJsonAsync<ContractDto>(Json))!;

        var response = await Client.DeleteAsync($"/api/contracts/{dto.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var row = await WithUowAsync(uow =>
            uow.RepositoryFor<Contract>().FirstOrDefaultAsync(c => c.Id == dto.Id, ignoreQueryFilters: true));
        Assert.NotNull(row);
        Assert.NotNull(row.DeletedAt);

        var detail = await Client.GetAsync($"/api/contracts/{dto.Id}");
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);

        var listResponse = await Client.GetAsync("/api/contracts");
        listResponse.EnsureSuccessStatusCode();
        var page = await listResponse.Content.ReadFromJsonAsync<KeysetPage<ContractSummaryDto>>(Json);
        Assert.NotNull(page);
        Assert.DoesNotContain(page.Items, c => c.Id == dto.Id);
    }

    [Fact]
    public async Task DeleteContract_AlreadySoftDeleted_Returns404()
    {
        await SeedAdminAsync();
        var customerId = await SeedCustomerAsync();
        var managerId = await SeedClientManagerAsync();

        var created = await Client.PostAsJsonAsync("/api/contracts", new CreateContractCommand(
            Subject: "Doomed",
            CustomerId: customerId,
            Start: new DateOnly(2026, 1, 1),
            End: null));
        var dto = (await created.Content.ReadFromJsonAsync<ContractDto>(Json))!;

        var first = await Client.DeleteAsync($"/api/contracts/{dto.Id}");
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        var second = await Client.DeleteAsync($"/api/contracts/{dto.Id}");
        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
        var body = await second.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("ERR_CONTRACT_NOT_FOUND", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task DeleteContract_AsNonAdmin_ReturnsForbidden()
    {
        await SeedNonAdminAsync();

        var response = await Client.DeleteAsync($"/api/contracts/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteContract_NotFound_Returns404()
    {
        await SeedAdminAsync();

        var response = await Client.DeleteAsync($"/api/contracts/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("ERR_CONTRACT_NOT_FOUND", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task DeleteContract_NumberNotReusedByNextCreate()
    {
        await SeedAdminAsync();
        var customerId = await SeedCustomerAsync();
        var managerId = await SeedClientManagerAsync();

        var first = await Client.PostAsJsonAsync("/api/contracts", new CreateContractCommand(
            Subject: "First",
            CustomerId: customerId,
            Start: new DateOnly(2026, 1, 1),
            End: null));
        var firstDto = (await first.Content.ReadFromJsonAsync<ContractDto>(Json))!;
        Assert.Equal(1, firstDto.Number);

        var delete = await Client.DeleteAsync($"/api/contracts/{firstDto.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var second = await Client.PostAsJsonAsync("/api/contracts", new CreateContractCommand(
            Subject: "Second",
            CustomerId: customerId,
            Start: new DateOnly(2026, 2, 1),
            End: null));
        var secondDto = (await second.Content.ReadFromJsonAsync<ContractDto>(Json))!;
        Assert.Equal(2, secondDto.Number);
    }

    [Fact]
    public async Task DeleteUser_LinkedAsClientManagerOnContract_Returns409()
    {
        await SeedAdminAsync();
        var customerId = await SeedCustomerAsync();
        var managerId = await SeedClientManagerAsync();

        await WithUowAsync(async uow =>
        {
            var contract = Contract.Create(
                number: 1,
                subject: "Engagement",
                customerId: customerId,
                clientManagerId: managerId,
                start: new DateOnly(2026, 1, 1));
            uow.RepositoryFor<Contract>().Add(contract);
            await uow.SaveChangesAsync();
        });

        var response = await Client.DeleteAsync($"/api/users/{managerId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("ERR_USER_CANNOT_REMOVE_CLIENT_MANAGER_ROLE_WHILE_ASSIGNED",
            body.GetProperty("code").GetString());
    }

    private async Task<ContractDto> SeedContractAsync(Guid customerId, Guid managerId, int number = 1)
    {
        var id = Guid.NewGuid();
        await WithUowAsync(async uow =>
        {
            var contract = Contract.Create(number, $"Engagement {number}", customerId, managerId,
                new DateOnly(2026, 1, 1));
            contract.Id = id;
            uow.RepositoryFor<Contract>().Add(contract);
            await uow.SaveChangesAsync();
        });
        var refreshed = await Client.GetAsync($"/api/contracts/{id}");
        return (await refreshed.Content.ReadFromJsonAsync<ContractDto>(Json))!;
    }

    [Fact]
    public async Task GetContractById_AsAdmin_Existing_Returns200_WithDto()
    {
        await SeedAdminAsync();
        var customerId = await SeedCustomerAsync();
        var managerId = await SeedClientManagerAsync();
        var seeded = await SeedContractAsync(customerId, managerId);

        var response = await Client.GetAsync($"/api/contracts/{seeded.Id}");

        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<ContractDto>(Json);
        Assert.NotNull(dto);
        Assert.Equal(seeded.Id, dto.Id);
        Assert.Equal(seeded.Number, dto.Number);
        Assert.Equal(seeded.Subject, dto.Subject);
        Assert.Equal(customerId, dto.CustomerId);
        Assert.Equal(managerId, dto.ClientManagerId);
    }

    [Fact]
    public async Task GetContractById_NotFound_Returns404()
    {
        await SeedAdminAsync();

        var response = await Client.GetAsync($"/api/contracts/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetContractById_SoftDeleted_Returns404()
    {
        await SeedAdminAsync();
        var customerId = await SeedCustomerAsync();
        var managerId = await SeedClientManagerAsync();
        var seeded = await SeedContractAsync(customerId, managerId);

        await Client.DeleteAsync($"/api/contracts/{seeded.Id}");

        var response = await Client.GetAsync($"/api/contracts/{seeded.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetContractById_AsNonAdmin_ReturnsForbidden()
    {
        await SeedNonAdminAsync();

        var response = await Client.GetAsync($"/api/contracts/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateContract_AsAdmin_Valid_Returns200_AndPersistsChanges()
    {
        await SeedAdminAsync();
        var customerId = await SeedCustomerAsync();
        var managerId = await SeedClientManagerAsync();
        var newManagerId = await SeedClientManagerAsync("cm2@test.com");
        var seeded = await SeedContractAsync(customerId, managerId);

        var update = new UpdateContractCommand(
            Id: seeded.Id,
            Subject: "Renamed",
            ClientManagerId: newManagerId,
            Start: new DateOnly(2026, 3, 1),
            End: new DateOnly(2026, 12, 31),
            ConsultantIds: []);

        var response = await Client.PutAsJsonAsync($"/api/contracts/{seeded.Id}", update);

        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<ContractDto>(Json);
        Assert.NotNull(dto);
        Assert.Equal(seeded.Id, dto.Id);
        Assert.Equal(seeded.Number, dto.Number);
        Assert.Equal("Renamed", dto.Subject);
        Assert.Equal(customerId, dto.CustomerId);
        Assert.Equal(newManagerId, dto.ClientManagerId);
        Assert.Equal(new DateOnly(2026, 3, 1), dto.Start);
        Assert.Equal(new DateOnly(2026, 12, 31), dto.End);
    }

    [Fact]
    public async Task UpdateContract_RouteIdMismatch_ReturnsBadRequest()
    {
        await SeedAdminAsync();

        var update = new UpdateContractCommand(
            Id: Guid.NewGuid(),
            Subject: "x",
            ClientManagerId: Guid.NewGuid(),
            Start: new DateOnly(2026, 1, 1),
            End: null,
            ConsultantIds: []);

        var response = await Client.PutAsJsonAsync($"/api/contracts/{Guid.NewGuid()}", update);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateContract_NotFound_Returns404()
    {
        await SeedAdminAsync();
        var managerId = await SeedClientManagerAsync();
        var id = Guid.NewGuid();

        var response = await Client.PutAsJsonAsync($"/api/contracts/{id}", new UpdateContractCommand(
            Id: id,
            Subject: "x",
            ClientManagerId: managerId,
            Start: new DateOnly(2026, 1, 1),
            End: null,
            ConsultantIds: []));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("ERR_CONTRACT_NOT_FOUND", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task UpdateContract_AsNonAdmin_ReturnsForbidden()
    {
        await SeedNonAdminAsync();
        var id = Guid.NewGuid();

        var response = await Client.PutAsJsonAsync($"/api/contracts/{id}", new UpdateContractCommand(
            Id: id,
            Subject: "x",
            ClientManagerId: Guid.NewGuid(),
            Start: new DateOnly(2026, 1, 1),
            End: null,
            ConsultantIds: []));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateContract_EmptySubject_Returns400()
    {
        await SeedAdminAsync();
        var customerId = await SeedCustomerAsync();
        var managerId = await SeedClientManagerAsync();
        var seeded = await SeedContractAsync(customerId, managerId);

        var response = await Client.PutAsJsonAsync($"/api/contracts/{seeded.Id}", new UpdateContractCommand(
            Id: seeded.Id,
            Subject: "",
            ClientManagerId: managerId,
            Start: new DateOnly(2026, 1, 1),
            End: null,
            ConsultantIds: []));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateContract_EndBeforeStart_Returns400()
    {
        await SeedAdminAsync();
        var customerId = await SeedCustomerAsync();
        var managerId = await SeedClientManagerAsync();
        var seeded = await SeedContractAsync(customerId, managerId);

        var response = await Client.PutAsJsonAsync($"/api/contracts/{seeded.Id}", new UpdateContractCommand(
            Id: seeded.Id,
            Subject: "Still valid",
            ClientManagerId: managerId,
            Start: new DateOnly(2026, 6, 1),
            End: new DateOnly(2026, 5, 31),
            ConsultantIds: []));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("ERR_CONTRACT_END_BEFORE_START", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task UpdateContract_ClientManagerNotFound_Returns404()
    {
        await SeedAdminAsync();
        var customerId = await SeedCustomerAsync();
        var managerId = await SeedClientManagerAsync();
        var seeded = await SeedContractAsync(customerId, managerId);

        var response = await Client.PutAsJsonAsync($"/api/contracts/{seeded.Id}", new UpdateContractCommand(
            Id: seeded.Id,
            Subject: "Subject",
            ClientManagerId: Guid.NewGuid(),
            Start: new DateOnly(2026, 1, 1),
            End: null,
            ConsultantIds: []));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("ERR_CONTRACT_CLIENT_MANAGER_NOT_FOUND", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task UpdateContract_ClientManagerMissingRole_Returns400()
    {
        await SeedAdminAsync();
        var customerId = await SeedCustomerAsync();
        var managerId = await SeedClientManagerAsync();
        var seeded = await SeedContractAsync(customerId, managerId);

        var plainUserId = Guid.NewGuid();
        await WithUowAsync(async uow =>
        {
            var user = User.Create("Plain", "User", "plain@test.com", [UserRole.User]);
            user.Id = plainUserId;
            uow.RepositoryFor<User>().Add(user);
            await uow.SaveChangesAsync();
        });

        var response = await Client.PutAsJsonAsync($"/api/contracts/{seeded.Id}", new UpdateContractCommand(
            Id: seeded.Id,
            Subject: "Subject",
            ClientManagerId: plainUserId,
            Start: new DateOnly(2026, 1, 1),
            End: null,
            ConsultantIds: []));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("ERR_CONTRACT_CLIENT_MANAGER_MISSING_ROLE", body.GetProperty("code").GetString());
    }

    private async Task<Guid> SeedCustomerAsync(string name, int number)
    {
        var id = Guid.NewGuid();
        await WithUowAsync(async uow =>
        {
            var customer = Customer.Create(number, name, ContactPerson.Create(null, $"c{number}@x.com"));
            customer.Id = id;
            uow.RepositoryFor<Customer>().Add(customer);
            await uow.SaveChangesAsync();
        });
        return id;
    }

    private Task SeedContractDirectAsync(Guid customerId, Guid managerId, int number, string subject, DateOnly start, DateOnly? end) =>
        WithUowAsync(async uow =>
        {
            var contract = Contract.Create(number, subject, customerId, managerId, start, end);
            uow.RepositoryFor<Contract>().Add(contract);
            await uow.SaveChangesAsync();
        });

    [Fact]
    public async Task GetContracts_AsNonAdmin_ReturnsForbidden()
    {
        await SeedNonAdminAsync();

        var response = await Client.GetAsync("/api/contracts");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetContracts_AsAdmin_ReturnsPagedSummaryWithZeroCounts()
    {
        await SeedAdminAsync();
        var customerId = await SeedCustomerAsync("Acme", 1);
        var managerId = await SeedClientManagerAsync();
        await SeedContractDirectAsync(customerId, managerId, 1, "Engagement Alpha", new DateOnly(2026, 1, 1), null);

        var response = await Client.GetAsync("/api/contracts");

        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<KeysetPage<ContractSummaryDto>>(Json);
        Assert.NotNull(page);
        Assert.Single(page.Items);
        Assert.Equal(1, page.Total);
        Assert.Equal("Engagement Alpha", page.Items[0].Subject);
        Assert.Equal(customerId, page.Items[0].CustomerId);
        Assert.Equal(0, page.Items[0].ActiveTaskCount);
        Assert.Equal(0, page.Items[0].ConsultantCount);
    }

    [Fact]
    public async Task GetContracts_PaginatesByPageSize()
    {
        await SeedAdminAsync();
        var customerId = await SeedCustomerAsync("Acme", 1);
        var managerId = await SeedClientManagerAsync();
        for (var i = 1; i <= 12; i++)
            await SeedContractDirectAsync(customerId, managerId, i, $"C {i:D2}", new DateOnly(2026, 1, 1), null);

        var first = await Client.GetAsync("/api/contracts?pageSize=10&sortBy=number");
        first.EnsureSuccessStatusCode();
        var firstPage = await first.Content.ReadFromJsonAsync<KeysetPage<ContractSummaryDto>>(Json);
        Assert.NotNull(firstPage);
        Assert.Equal(10, firstPage.Items.Count);
        Assert.Equal(12, firstPage.Total);
        Assert.NotNull(firstPage.NextCursor);

        var second = await Client.GetAsync(
            $"/api/contracts?pageSize=10&sortBy=number&cursor={Uri.EscapeDataString(firstPage.NextCursor)}");
        second.EnsureSuccessStatusCode();
        var secondPage = await second.Content.ReadFromJsonAsync<KeysetPage<ContractSummaryDto>>(Json);
        Assert.NotNull(secondPage);
        Assert.Equal(2, secondPage.Items.Count);
        Assert.Null(secondPage.NextCursor);
    }

    [Fact]
    public async Task GetContracts_SearchMatchesSubject()
    {
        await SeedAdminAsync();
        var customerId = await SeedCustomerAsync("Acme", 1);
        var managerId = await SeedClientManagerAsync();
        await SeedContractDirectAsync(customerId, managerId, 1, "Alpha Engagement", new DateOnly(2026, 1, 1), null);
        await SeedContractDirectAsync(customerId, managerId, 2, "Other", new DateOnly(2026, 1, 1), null);

        var response = await Client.GetAsync("/api/contracts?search=alpha");

        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<KeysetPage<ContractSummaryDto>>(Json);
        Assert.NotNull(page);
        Assert.Single(page.Items);
        Assert.Equal("Alpha Engagement", page.Items[0].Subject);
    }

    [Fact]
    public async Task GetContracts_SearchMatchesCustomerName()
    {
        await SeedAdminAsync();
        var matchingCustomerId = await SeedCustomerAsync("Brussels Co", 1);
        var otherCustomerId = await SeedCustomerAsync("Acme", 2);
        var managerId = await SeedClientManagerAsync();
        await SeedContractDirectAsync(matchingCustomerId, managerId, 1, "Generic", new DateOnly(2026, 1, 1), null);
        await SeedContractDirectAsync(otherCustomerId, managerId, 2, "Generic", new DateOnly(2026, 1, 1), null);

        var response = await Client.GetAsync("/api/contracts?search=brussels");

        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<KeysetPage<ContractSummaryDto>>(Json);
        Assert.NotNull(page);
        Assert.Single(page.Items);
        Assert.Equal(matchingCustomerId, page.Items[0].CustomerId);
    }

    [Fact]
    public async Task GetContracts_ActiveOnDate_FiltersByPeriod()
    {
        await SeedAdminAsync();
        var customerId = await SeedCustomerAsync("Acme", 1);
        var managerId = await SeedClientManagerAsync();
        await SeedContractDirectAsync(customerId, managerId, 1, "Open ended", new DateOnly(2026, 1, 1), null);
        await SeedContractDirectAsync(customerId, managerId, 2, "Ended", new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31));
        await SeedContractDirectAsync(customerId, managerId, 3, "Future", new DateOnly(2026, 6, 1), null);

        var response = await Client.GetAsync("/api/contracts?activeOnDate=2026-05-01");

        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<KeysetPage<ContractSummaryDto>>(Json);
        Assert.NotNull(page);
        Assert.Single(page.Items);
        Assert.Equal(1, page.Items[0].Number);
    }

    [Fact]
    public async Task GetContracts_CustomerIdFilter_ReturnsOnlyMatching()
    {
        await SeedAdminAsync();
        var targetCustomerId = await SeedCustomerAsync("Acme", 1);
        var otherCustomerId = await SeedCustomerAsync("Other", 2);
        var managerId = await SeedClientManagerAsync();
        await SeedContractDirectAsync(targetCustomerId, managerId, 1, "Mine", new DateOnly(2026, 1, 1), null);
        await SeedContractDirectAsync(otherCustomerId, managerId, 2, "Not mine", new DateOnly(2026, 1, 1), null);

        var response = await Client.GetAsync($"/api/contracts?customerId={targetCustomerId}");

        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<KeysetPage<ContractSummaryDto>>(Json);
        Assert.NotNull(page);
        Assert.Single(page.Items);
        Assert.Equal(targetCustomerId, page.Items[0].CustomerId);
    }

    [Fact]
    public async Task GetContracts_CombinedFilters_Intersect()
    {
        await SeedAdminAsync();
        var customerA = await SeedCustomerAsync("Acme", 1);
        var customerB = await SeedCustomerAsync("Beta", 2);
        var managerId = await SeedClientManagerAsync();
        await SeedContractDirectAsync(customerA, managerId, 1, "Alpha", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        await SeedContractDirectAsync(customerA, managerId, 2, "Beta", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        await SeedContractDirectAsync(customerB, managerId, 3, "Alpha", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        var response = await Client.GetAsync(
            $"/api/contracts?search=alpha&customerId={customerA}&activeOnDate=2026-06-01");

        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<KeysetPage<ContractSummaryDto>>(Json);
        Assert.NotNull(page);
        Assert.Single(page.Items);
        Assert.Equal(1, page.Items[0].Number);
    }

    [Fact]
    public async Task GetContracts_SoftDeletedExcluded()
    {
        await SeedAdminAsync();
        var customerId = await SeedCustomerAsync("Acme", 1);
        var managerId = await SeedClientManagerAsync();
        var seeded = await SeedContractAsync(customerId, managerId);
        await Client.DeleteAsync($"/api/contracts/{seeded.Id}");

        var response = await Client.GetAsync("/api/contracts");

        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<KeysetPage<ContractSummaryDto>>(Json);
        Assert.NotNull(page);
        Assert.Empty(page.Items);
        Assert.Equal(0, page.Total);
    }

    private async Task<Guid> SeedConsultantAsync(string email)
    {
        var id = Guid.NewGuid();
        await WithUowAsync(async uow =>
        {
            var user = User.Create("Cons", "Ultant", email, [UserRole.User]);
            user.Id = id;
            uow.RepositoryFor<User>().Add(user);
            await uow.SaveChangesAsync();
        });
        return id;
    }

    [Fact]
    public async Task UpdateContract_AddConsultants_Persists_AndDtoReflectsSet()
    {
        await SeedAdminAsync();
        var customerId = await SeedCustomerAsync();
        var managerId = await SeedClientManagerAsync();
        var seeded = await SeedContractAsync(customerId, managerId);
        var c1 = await SeedConsultantAsync("c1@test.com");
        var c2 = await SeedConsultantAsync("c2@test.com");

        var response = await Client.PutAsJsonAsync($"/api/contracts/{seeded.Id}", new UpdateContractCommand(
            Id: seeded.Id,
            Subject: seeded.Subject,
            ClientManagerId: managerId,
            Start: seeded.Start,
            End: seeded.End,
            ConsultantIds: [c1, c2]));

        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<ContractDto>(Json);
        Assert.NotNull(dto);
        Assert.Equal(new[] { c1, c2 }.OrderBy(g => g), dto.ConsultantIds.OrderBy(g => g));

        var refreshed = await Client.GetAsync($"/api/contracts/{seeded.Id}");
        var reread = await refreshed.Content.ReadFromJsonAsync<ContractDto>(Json);
        Assert.Equal(new[] { c1, c2 }.OrderBy(g => g), reread!.ConsultantIds.OrderBy(g => g));

        var listResp = await Client.GetAsync("/api/contracts");
        var page = await listResp.Content.ReadFromJsonAsync<KeysetPage<ContractSummaryDto>>(Json);
        Assert.Equal(2, page!.Items.Single().ConsultantCount);
    }

    [Fact]
    public async Task UpdateContract_RemoveConsultant_Persists()
    {
        await SeedAdminAsync();
        var customerId = await SeedCustomerAsync();
        var managerId = await SeedClientManagerAsync();
        var seeded = await SeedContractAsync(customerId, managerId);
        var c1 = await SeedConsultantAsync("c1@test.com");
        var c2 = await SeedConsultantAsync("c2@test.com");

        await Client.PutAsJsonAsync($"/api/contracts/{seeded.Id}", new UpdateContractCommand(
            seeded.Id, seeded.Subject, managerId, seeded.Start, seeded.End, [c1, c2]));

        var response = await Client.PutAsJsonAsync($"/api/contracts/{seeded.Id}", new UpdateContractCommand(
            seeded.Id, seeded.Subject, managerId, seeded.Start, seeded.End, [c1]));

        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<ContractDto>(Json);
        Assert.Equal(new[] { c1 }, dto!.ConsultantIds);
    }

    [Fact]
    public async Task UpdateContract_IdempotentResubmit_NoChanges()
    {
        await SeedAdminAsync();
        var customerId = await SeedCustomerAsync();
        var managerId = await SeedClientManagerAsync();
        var seeded = await SeedContractAsync(customerId, managerId);
        var c1 = await SeedConsultantAsync("c1@test.com");

        await Client.PutAsJsonAsync($"/api/contracts/{seeded.Id}", new UpdateContractCommand(
            seeded.Id, seeded.Subject, managerId, seeded.Start, seeded.End, [c1]));
        var response = await Client.PutAsJsonAsync($"/api/contracts/{seeded.Id}", new UpdateContractCommand(
            seeded.Id, seeded.Subject, managerId, seeded.Start, seeded.End, [c1]));

        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<ContractDto>(Json);
        Assert.Equal(new[] { c1 }, dto!.ConsultantIds);
    }

    [Fact]
    public async Task UpdateContract_DuplicateConsultantIds_Returns400()
    {
        await SeedAdminAsync();
        var customerId = await SeedCustomerAsync();
        var managerId = await SeedClientManagerAsync();
        var seeded = await SeedContractAsync(customerId, managerId);
        var c1 = await SeedConsultantAsync("c1@test.com");

        var response = await Client.PutAsJsonAsync($"/api/contracts/{seeded.Id}", new UpdateContractCommand(
            seeded.Id, seeded.Subject, managerId, seeded.Start, seeded.End, [c1, c1]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("ERR_CONTRACT_CONSULTANT_DUPLICATES", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task UpdateContract_AddSoftDeletedUser_Returns404()
    {
        await SeedAdminAsync();
        var customerId = await SeedCustomerAsync();
        var managerId = await SeedClientManagerAsync();
        var seeded = await SeedContractAsync(customerId, managerId);
        var ghost = await SeedConsultantAsync("ghost@test.com");

        await WithUowAsync(async uow =>
        {
            var u = await uow.RepositoryFor<User>().GetByIdAsync(ghost);
            u!.SoftDelete(DateTimeOffset.UtcNow);
            await uow.SaveChangesAsync();
        });

        var response = await Client.PutAsJsonAsync($"/api/contracts/{seeded.Id}", new UpdateContractCommand(
            seeded.Id, seeded.Subject, managerId, seeded.Start, seeded.End, [ghost]));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("ERR_CONTRACT_CONSULTANT_NOT_FOUND", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task UpdateContract_KeepingDeletedConsultant_Succeeds_PreservingHistory()
    {
        await SeedAdminAsync();
        var customerId = await SeedCustomerAsync();
        var managerId = await SeedClientManagerAsync();
        var seeded = await SeedContractAsync(customerId, managerId);
        var consultant = await SeedConsultantAsync("c@test.com");

        // Assign consultant first.
        await Client.PutAsJsonAsync($"/api/contracts/{seeded.Id}", new UpdateContractCommand(
            seeded.Id, seeded.Subject, managerId, seeded.Start, seeded.End, [consultant]));

        // Soft-delete the user directly.
        await WithUowAsync(async uow =>
        {
            var u = await uow.RepositoryFor<User>().GetByIdAsync(consultant);
            u!.SoftDelete(DateTimeOffset.UtcNow);
            await uow.SaveChangesAsync();
        });

        // Make an unrelated change while keeping the now-deleted consultant on the contract.
        var response = await Client.PutAsJsonAsync($"/api/contracts/{seeded.Id}", new UpdateContractCommand(
            seeded.Id, "Renamed while keeping ghost", managerId, seeded.Start, seeded.End, [consultant]));

        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<ContractDto>(Json);
        Assert.Equal("Renamed while keeping ghost", dto!.Subject);
        Assert.Equal(new[] { consultant }, dto.ConsultantIds);
    }

    [Fact]
    public async Task UpdateContract_AddTask_Returns200_AndIncludesIt()
    {
        await SeedAdminAsync();
        var customerId = await SeedCustomerAsync();
        var managerId = await SeedClientManagerAsync();
        var seeded = await SeedContractAsync(customerId, managerId);

        var response = await Client.PutAsJsonAsync($"/api/contracts/{seeded.Id}", new UpdateContractCommand(
            seeded.Id, seeded.Subject, managerId, seeded.Start, seeded.End,
            ConsultantIds: [],
            Tasks: [new UpdateContractTaskDto(null, "Build", 100m)]));

        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<ContractDto>(Json);
        Assert.NotNull(dto);
        Assert.Single(dto.Tasks);
        var task = dto.Tasks.Single();
        Assert.NotEqual(Guid.Empty, task.Id);
        Assert.Equal("Build", task.Name);
        Assert.Equal(100m, task.Rate);
        Assert.Null(task.DeletedAt);
    }

    [Fact]
    public async Task UpdateContract_EditTask_Returns200_AndUpdatesNameAndRate()
    {
        await SeedAdminAsync();
        var customerId = await SeedCustomerAsync();
        var managerId = await SeedClientManagerAsync();
        var seeded = await SeedContractAsync(customerId, managerId);

        var added = await Client.PutAsJsonAsync($"/api/contracts/{seeded.Id}", new UpdateContractCommand(
            seeded.Id, seeded.Subject, managerId, seeded.Start, seeded.End,
            ConsultantIds: [],
            Tasks: [new UpdateContractTaskDto(null, "Build", 100m)]));
        var addedDto = (await added.Content.ReadFromJsonAsync<ContractDto>(Json))!;
        var taskId = addedDto.Tasks.Single().Id;

        var response = await Client.PutAsJsonAsync($"/api/contracts/{seeded.Id}", new UpdateContractCommand(
            seeded.Id, seeded.Subject, managerId, seeded.Start, seeded.End,
            ConsultantIds: [],
            Tasks: [new UpdateContractTaskDto(taskId, "Build v2", 130m)]));

        response.EnsureSuccessStatusCode();
        var dto = (await response.Content.ReadFromJsonAsync<ContractDto>(Json))!;
        var task = dto.Tasks.Single();
        Assert.Equal(taskId, task.Id);
        Assert.Equal("Build v2", task.Name);
        Assert.Equal(130m, task.Rate);
        Assert.Null(task.DeletedAt);
    }

    [Fact]
    public async Task UpdateContract_RemoveTask_SoftDeletesIt_StillInDto()
    {
        await SeedAdminAsync();
        var customerId = await SeedCustomerAsync();
        var managerId = await SeedClientManagerAsync();
        var seeded = await SeedContractAsync(customerId, managerId);

        var added = await Client.PutAsJsonAsync($"/api/contracts/{seeded.Id}", new UpdateContractCommand(
            seeded.Id, seeded.Subject, managerId, seeded.Start, seeded.End,
            ConsultantIds: [],
            Tasks: [new UpdateContractTaskDto(null, "Build", 100m)]));
        var addedDto = (await added.Content.ReadFromJsonAsync<ContractDto>(Json))!;
        var taskId = addedDto.Tasks.Single().Id;

        var response = await Client.PutAsJsonAsync($"/api/contracts/{seeded.Id}", new UpdateContractCommand(
            seeded.Id, seeded.Subject, managerId, seeded.Start, seeded.End,
            ConsultantIds: [],
            Tasks: []));

        response.EnsureSuccessStatusCode();
        var dto = (await response.Content.ReadFromJsonAsync<ContractDto>(Json))!;
        Assert.Single(dto.Tasks);
        var archived = dto.Tasks.Single();
        Assert.Equal(taskId, archived.Id);
        Assert.NotNull(archived.DeletedAt);
    }

    [Fact]
    public async Task GetContractById_AfterSoftDeleteTask_IncludesArchivedTaskInDto()
    {
        await SeedAdminAsync();
        var customerId = await SeedCustomerAsync();
        var managerId = await SeedClientManagerAsync();
        var seeded = await SeedContractAsync(customerId, managerId);

        var added = await Client.PutAsJsonAsync($"/api/contracts/{seeded.Id}", new UpdateContractCommand(
            seeded.Id, seeded.Subject, managerId, seeded.Start, seeded.End,
            ConsultantIds: [],
            Tasks: [new UpdateContractTaskDto(null, "Build", 100m)]));
        var addedDto = (await added.Content.ReadFromJsonAsync<ContractDto>(Json))!;
        var taskId = addedDto.Tasks.Single().Id;

        await Client.PutAsJsonAsync($"/api/contracts/{seeded.Id}", new UpdateContractCommand(
            seeded.Id, seeded.Subject, managerId, seeded.Start, seeded.End,
            ConsultantIds: [],
            Tasks: []));

        var getResponse = await Client.GetAsync($"/api/contracts/{seeded.Id}");
        getResponse.EnsureSuccessStatusCode();
        var dto = (await getResponse.Content.ReadFromJsonAsync<ContractDto>(Json))!;
        Assert.Single(dto.Tasks);
        var archived = dto.Tasks.Single();
        Assert.Equal(taskId, archived.Id);
        Assert.NotNull(archived.DeletedAt);
    }

    [Fact]
    public async Task UpdateContract_ResurrectByName_PreservesId_OverwritesCasingAndRate()
    {
        await SeedAdminAsync();
        var customerId = await SeedCustomerAsync();
        var managerId = await SeedClientManagerAsync();
        var seeded = await SeedContractAsync(customerId, managerId);

        var added = await Client.PutAsJsonAsync($"/api/contracts/{seeded.Id}", new UpdateContractCommand(
            seeded.Id, seeded.Subject, managerId, seeded.Start, seeded.End,
            ConsultantIds: [],
            Tasks: [new UpdateContractTaskDto(null, "Build", 100m)]));
        var addedDto = (await added.Content.ReadFromJsonAsync<ContractDto>(Json))!;
        var originalId = addedDto.Tasks.Single().Id;

        await Client.PutAsJsonAsync($"/api/contracts/{seeded.Id}", new UpdateContractCommand(
            seeded.Id, seeded.Subject, managerId, seeded.Start, seeded.End,
            ConsultantIds: [],
            Tasks: []));

        var response = await Client.PutAsJsonAsync($"/api/contracts/{seeded.Id}", new UpdateContractCommand(
            seeded.Id, seeded.Subject, managerId, seeded.Start, seeded.End,
            ConsultantIds: [],
            Tasks: [new UpdateContractTaskDto(null, "BUILD", 200m)]));

        response.EnsureSuccessStatusCode();
        var dto = (await response.Content.ReadFromJsonAsync<ContractDto>(Json))!;
        Assert.Single(dto.Tasks);
        var resurrected = dto.Tasks.Single();
        Assert.Equal(originalId, resurrected.Id);
        Assert.Equal("BUILD", resurrected.Name);
        Assert.Equal(200m, resurrected.Rate);
        Assert.Null(resurrected.DeletedAt);
    }

    [Fact]
    public async Task UpdateContract_DuplicateActiveTaskNames_Returns400()
    {
        await SeedAdminAsync();
        var customerId = await SeedCustomerAsync();
        var managerId = await SeedClientManagerAsync();
        var seeded = await SeedContractAsync(customerId, managerId);

        var response = await Client.PutAsJsonAsync($"/api/contracts/{seeded.Id}", new UpdateContractCommand(
            seeded.Id, seeded.Subject, managerId, seeded.Start, seeded.End,
            ConsultantIds: [],
            Tasks: [
                new UpdateContractTaskDto(null, "Build", 100m),
                new UpdateContractTaskDto(null, "build", 110m),
            ]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("ERR_CONTRACT_DUPLICATE_TASK_NAME", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task UpdateContract_UnknownTaskId_Returns400()
    {
        await SeedAdminAsync();
        var customerId = await SeedCustomerAsync();
        var managerId = await SeedClientManagerAsync();
        var seeded = await SeedContractAsync(customerId, managerId);

        var response = await Client.PutAsJsonAsync($"/api/contracts/{seeded.Id}", new UpdateContractCommand(
            seeded.Id, seeded.Subject, managerId, seeded.Start, seeded.End,
            ConsultantIds: [],
            Tasks: [new UpdateContractTaskDto(Guid.NewGuid(), "Build", 100m)]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("ERR_CONTRACT_TASK_NOT_FOUND", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task UpdateContract_FullCycle_Create_Edit_Remove_Resurrect()
    {
        await SeedAdminAsync();
        var customerId = await SeedCustomerAsync();
        var managerId = await SeedClientManagerAsync();
        var seeded = await SeedContractAsync(customerId, managerId);

        // Create
        var r1 = await Client.PutAsJsonAsync($"/api/contracts/{seeded.Id}", new UpdateContractCommand(
            seeded.Id, seeded.Subject, managerId, seeded.Start, seeded.End,
            ConsultantIds: [],
            Tasks: [new UpdateContractTaskDto(null, "Build", 100m)]));
        var d1 = (await r1.Content.ReadFromJsonAsync<ContractDto>(Json))!;
        var taskId = d1.Tasks.Single().Id;

        // Edit (rename + new rate)
        var r2 = await Client.PutAsJsonAsync($"/api/contracts/{seeded.Id}", new UpdateContractCommand(
            seeded.Id, seeded.Subject, managerId, seeded.Start, seeded.End,
            ConsultantIds: [],
            Tasks: [new UpdateContractTaskDto(taskId, "Implementation", 125m)]));
        var d2 = (await r2.Content.ReadFromJsonAsync<ContractDto>(Json))!;
        Assert.Equal(taskId, d2.Tasks.Single().Id);
        Assert.Equal("Implementation", d2.Tasks.Single().Name);

        // Remove
        var r3 = await Client.PutAsJsonAsync($"/api/contracts/{seeded.Id}", new UpdateContractCommand(
            seeded.Id, seeded.Subject, managerId, seeded.Start, seeded.End,
            ConsultantIds: [],
            Tasks: []));
        var d3 = (await r3.Content.ReadFromJsonAsync<ContractDto>(Json))!;
        Assert.NotNull(d3.Tasks.Single().DeletedAt);

        // Resurrect by name (case-insensitive, new rate, new casing)
        var r4 = await Client.PutAsJsonAsync($"/api/contracts/{seeded.Id}", new UpdateContractCommand(
            seeded.Id, seeded.Subject, managerId, seeded.Start, seeded.End,
            ConsultantIds: [],
            Tasks: [new UpdateContractTaskDto(null, "IMPLEMENTATION", 200m)]));
        var d4 = (await r4.Content.ReadFromJsonAsync<ContractDto>(Json))!;
        var resurrected = d4.Tasks.Single();
        Assert.Equal(taskId, resurrected.Id);
        Assert.Equal("IMPLEMENTATION", resurrected.Name);
        Assert.Equal(200m, resurrected.Rate);
        Assert.Null(resurrected.DeletedAt);
    }

    [Fact]
    public async Task UpdateContract_NullTasks_LeavesExistingTasksUntouched()
    {
        await SeedAdminAsync();
        var customerId = await SeedCustomerAsync();
        var managerId = await SeedClientManagerAsync();
        var seeded = await SeedContractAsync(customerId, managerId);

        // Seed a task.
        await Client.PutAsJsonAsync($"/api/contracts/{seeded.Id}", new UpdateContractCommand(
            seeded.Id, seeded.Subject, managerId, seeded.Start, seeded.End,
            ConsultantIds: [],
            Tasks: [new UpdateContractTaskDto(null, "Build", 100m)]));

        // Update without Tasks (null).
        var response = await Client.PutAsJsonAsync($"/api/contracts/{seeded.Id}", new UpdateContractCommand(
            seeded.Id, "Renamed", managerId, seeded.Start, seeded.End,
            ConsultantIds: [],
            Tasks: null));

        response.EnsureSuccessStatusCode();
        var dto = (await response.Content.ReadFromJsonAsync<ContractDto>(Json))!;
        Assert.Equal("Renamed", dto.Subject);
        Assert.Single(dto.Tasks);
        Assert.Equal("Build", dto.Tasks.Single().Name);
        Assert.Null(dto.Tasks.Single().DeletedAt);
    }

    [Fact]
    public async Task GetContractById_AsAssignedClientManager_Returns200()
    {
        var cmId = await SeedClientManagerAsCallerAsync();
        var customerId = await SeedCustomerAsync();
        var contract = await SeedContractAsync(customerId, cmId);

        var response = await Client.GetAsync($"/api/contracts/{contract.Id}");

        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<ContractDto>(Json);
        Assert.NotNull(dto);
        Assert.Equal(contract.Id, dto.Id);
    }

    [Fact]
    public async Task GetContractById_AsUnassignedClientManager_Returns200()
    {
        var callerId = await SeedClientManagerAsCallerAsync();
        var otherCmId = await SeedClientManagerAsync("other-cm@test.com");
        var customerId = await SeedCustomerAsync();
        var contract = await SeedContractAsync(customerId, otherCmId);

        var response = await Client.GetAsync($"/api/contracts/{contract.Id}");

        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<ContractDto>(Json);
        Assert.NotNull(dto);
        Assert.Equal(contract.Id, dto.Id);
    }

    [Fact]
    public async Task GetContracts_AsClientManager_ReturnsAllContracts()
    {
        var callerId = await SeedClientManagerAsCallerAsync();
        var otherCmId = await SeedClientManagerAsync("other-cm@test.com");
        var customerId = await SeedCustomerAsync();
        await SeedContractDirectAsync(customerId, callerId, 1, "Mine", new DateOnly(2026, 1, 1), null);
        await SeedContractDirectAsync(customerId, otherCmId, 2, "Theirs", new DateOnly(2026, 1, 1), null);

        var response = await Client.GetAsync("/api/contracts");

        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<KeysetPage<ContractSummaryDto>>(Json);
        Assert.NotNull(page);
        Assert.Equal(2, page.Items.Count);
    }

    [Fact]
    public async Task CreateContract_AsClientManager_Returns403()
    {
        await SeedClientManagerAsCallerAsync();
        var customerId = await SeedCustomerAsync();

        var response = await Client.PostAsJsonAsync("/api/contracts", new CreateContractCommand(
            Subject: "Engagement",
            CustomerId: customerId,
            Start: new DateOnly(2026, 1, 1),
            End: null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteContract_AsClientManager_Returns403()
    {
        var cmId = await SeedClientManagerAsCallerAsync();
        var customerId = await SeedCustomerAsync();
        var contract = await SeedContractAsync(customerId, cmId);

        var response = await Client.DeleteAsync($"/api/contracts/{contract.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateContract_AsAssignedClientManager_Zone2Edits_Returns200()
    {
        var cmId = await SeedClientManagerAsCallerAsync();
        var customerId = await SeedCustomerAsync();
        var contract = await SeedContractAsync(customerId, cmId);
        var consultant = await SeedConsultantAsync("c1@test.com");

        var response = await Client.PutAsJsonAsync($"/api/contracts/{contract.Id}", new UpdateContractCommand(
            Id: contract.Id,
            Subject: contract.Subject,
            ClientManagerId: contract.ClientManagerId,
            Start: contract.Start,
            End: contract.End,
            ConsultantIds: [consultant],
            Tasks: [new UpdateContractTaskDto(null, "Build", 100m)]));

        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<ContractDto>(Json);
        Assert.NotNull(dto);
        Assert.Equal(new[] { consultant }, dto.ConsultantIds);
        Assert.Single(dto.Tasks);
        Assert.Equal("Build", dto.Tasks.Single().Name);
    }

    [Fact]
    public async Task UpdateContract_AsAssignedClientManager_UnchangedZone1_Returns200()
    {
        var cmId = await SeedClientManagerAsCallerAsync();
        var customerId = await SeedCustomerAsync();
        var contract = await SeedContractAsync(customerId, cmId);

        var response = await Client.PutAsJsonAsync($"/api/contracts/{contract.Id}", new UpdateContractCommand(
            Id: contract.Id,
            Subject: contract.Subject,
            ClientManagerId: contract.ClientManagerId,
            Start: contract.Start,
            End: contract.End,
            ConsultantIds: []));

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task UpdateContract_AsAssignedClientManager_ChangedSubject_Returns403()
    {
        var cmId = await SeedClientManagerAsCallerAsync();
        var customerId = await SeedCustomerAsync();
        var contract = await SeedContractAsync(customerId, cmId);

        var response = await Client.PutAsJsonAsync($"/api/contracts/{contract.Id}", new UpdateContractCommand(
            Id: contract.Id,
            Subject: "Renamed by CM",
            ClientManagerId: contract.ClientManagerId,
            Start: contract.Start,
            End: contract.End,
            ConsultantIds: []));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("ERR_CONTRACT_ZONE1_FIELD_NOT_EDITABLE", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task UpdateContract_AsAssignedClientManager_ChangedClientManager_Returns403()
    {
        var cmId = await SeedClientManagerAsCallerAsync();
        var otherCmId = await SeedClientManagerAsync("other-cm@test.com");
        var customerId = await SeedCustomerAsync();
        var contract = await SeedContractAsync(customerId, cmId);

        var response = await Client.PutAsJsonAsync($"/api/contracts/{contract.Id}", new UpdateContractCommand(
            Id: contract.Id,
            Subject: contract.Subject,
            ClientManagerId: otherCmId,
            Start: contract.Start,
            End: contract.End,
            ConsultantIds: []));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("ERR_CONTRACT_ZONE1_FIELD_NOT_EDITABLE", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task UpdateContract_AsAssignedClientManager_ChangedDates_Returns403()
    {
        var cmId = await SeedClientManagerAsCallerAsync();
        var customerId = await SeedCustomerAsync();
        var contract = await SeedContractAsync(customerId, cmId);

        var response = await Client.PutAsJsonAsync($"/api/contracts/{contract.Id}", new UpdateContractCommand(
            Id: contract.Id,
            Subject: contract.Subject,
            ClientManagerId: contract.ClientManagerId,
            Start: contract.Start.AddDays(7),
            End: contract.End,
            ConsultantIds: []));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("ERR_CONTRACT_ZONE1_FIELD_NOT_EDITABLE", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task UpdateContract_AsUnassignedClientManager_Returns403_WithEditNotAuthorized()
    {
        var callerId = await SeedClientManagerAsCallerAsync();
        var otherCmId = await SeedClientManagerAsync("other-cm@test.com");
        var customerId = await SeedCustomerAsync();
        var contract = await SeedContractAsync(customerId, otherCmId);

        var response = await Client.PutAsJsonAsync($"/api/contracts/{contract.Id}", new UpdateContractCommand(
            Id: contract.Id,
            Subject: contract.Subject,
            ClientManagerId: contract.ClientManagerId,
            Start: contract.Start,
            End: contract.End,
            ConsultantIds: []));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("ERR_CONTRACT_EDIT_NOT_AUTHORIZED", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task GetContracts_ActiveTaskCount_ReflectsOnlyNonDeleted()
    {
        await SeedAdminAsync();
        var customerId = await SeedCustomerAsync();
        var managerId = await SeedClientManagerAsync();
        var seeded = await SeedContractAsync(customerId, managerId);

        // Add two, soft-delete one.
        var added = await Client.PutAsJsonAsync($"/api/contracts/{seeded.Id}", new UpdateContractCommand(
            seeded.Id, seeded.Subject, managerId, seeded.Start, seeded.End,
            ConsultantIds: [],
            Tasks: [
                new UpdateContractTaskDto(null, "Build", 100m),
                new UpdateContractTaskDto(null, "Design", 80m),
            ]));
        var addedDto = (await added.Content.ReadFromJsonAsync<ContractDto>(Json))!;
        var keepId = addedDto.Tasks.Single(t => t.Name == "Build").Id;

        await Client.PutAsJsonAsync($"/api/contracts/{seeded.Id}", new UpdateContractCommand(
            seeded.Id, seeded.Subject, managerId, seeded.Start, seeded.End,
            ConsultantIds: [],
            Tasks: [new UpdateContractTaskDto(keepId, "Build", 100m)]));

        var listResp = await Client.GetAsync("/api/contracts");
        var page = (await listResp.Content.ReadFromJsonAsync<KeysetPage<ContractSummaryDto>>(Json))!;
        Assert.Equal(1, page.Items.Single().ActiveTaskCount);
    }
}
