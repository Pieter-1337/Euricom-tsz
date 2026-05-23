using System.Net;
using System.Net.Http.Json;
using Tsz.Api.Tests.Integration.TestAuth;
using Tsz.Modules.Contracts.Contracts.Queries;
using Tsz.Modules.Contracts.Domain.Contracts;
using Tsz.Modules.Customers.Domain.Customers;
using Tsz.Modules.Timesheets.Domain.Timesheets;
using Tsz.Modules.Timesheets.Features;
using Tsz.Modules.Users.Contracts;
using Tsz.Modules.Users.Domain.Users;

namespace Tsz.Api.Tests.Integration;

public class TimesheetEndpointsTests : IntegrationTestBase, IAsyncLifetime
{
    private const string TestOid = "test-user-oid";

    public TimesheetEndpointsTests(TestWebApplicationFactory factory) : base(factory) { }

    public async Task InitializeAsync()
    {
        await WithUowAsync(uow => uow.RepositoryFor<TimesheetWeek>().BatchHardDeleteAsync(_ => true));
        await WithUowAsync(uow => uow.RepositoryFor<Contract>().BatchHardDeleteAsync(_ => true));
        await WithUowAsync(uow => uow.RepositoryFor<Customer>().BatchHardDeleteAsync(_ => true));
        await WithUowAsync(uow => uow.RepositoryFor<User>().BatchHardDeleteAsync(_ => true));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<Guid> SeedUserAsync(params UserRole[] roles)
    {
        var id = Guid.NewGuid();
        await WithUowAsync(async uow =>
        {
            var user = User.Create("Test", "User", $"test-{id}@test.com", roles);
            user.Id = id;
            user.LinkEntraOid(TestOid);
            uow.RepositoryFor<User>().Add(user);
            await uow.SaveChangesAsync();
        });
        return id;
    }

    private async Task<Guid> SeedCustomerAsync()
    {
        var id = Guid.NewGuid();
        await WithUowAsync(async uow =>
        {
            var customer = Customer.Create(1, "Acme", ContactPerson.Create(null, "a@acme.com"));
            customer.Id = id;
            uow.RepositoryFor<Customer>().Add(customer);
            await uow.SaveChangesAsync();
        });
        return id;
    }

    private async Task<(Guid ContractId, Guid TaskId)> SeedContractWithTaskAsync(Guid userId, Guid customerId)
    {
        var contractId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        await WithUowAsync(async uow =>
        {
            var contract = Contract.Create(1, "Engagement", customerId, null,
                new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
            contract.Id = contractId;
            contract.ReplaceConsultants([userId]);
            contract.ApplyTasks([new UpdateContractTaskDto(null, "Development", 100m)], DateTimeOffset.UtcNow);
            uow.RepositoryFor<Contract>().Add(contract);
            await uow.SaveChangesAsync();

            // Retrieve the actual task id (auto-generated)
            var saved = await uow.RepositoryFor<Contract>().GetByIdAsync(contractId);
            taskId = saved!.Tasks.First().Id;
        });
        // Re-read
        await WithUowAsync(async uow =>
        {
            var saved = await uow.RepositoryFor<Contract>().GetByIdAsync(contractId);
            taskId = saved!.Tasks.First().Id;
        });
        return (contractId, taskId);
    }

    [Fact]
    public async Task GetTimesheetWeek_NoExistingRow_ReturnsEmptyDraft()
    {
        var userId = await SeedUserAsync(UserRole.User);

        var response = await Client.GetAsync($"/api/timesheet-weeks/{userId}/2026/21");

        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<TimesheetWeekDto>(Json);
        Assert.NotNull(dto);
        Assert.Null(dto.Id);
        Assert.Equal(nameof(TimesheetStatus.Draft), dto.Status);
        Assert.Equal(userId, dto.UserId);
        Assert.Equal(2026, dto.IsoYear);
        Assert.Equal(21, dto.IsoWeek);
        Assert.Equal(7, dto.Days.Count);
        Assert.Empty(dto.TimeEntries);
    }

    [Fact]
    public async Task GetTimesheetWeek_Days_HaveCorrectDates()
    {
        var userId = await SeedUserAsync(UserRole.User);

        var response = await Client.GetAsync($"/api/timesheet-weeks/{userId}/2026/21");

        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<TimesheetWeekDto>(Json);
        Assert.NotNull(dto);
        // ISO week 21 2026: Mon May 18 - Sun May 24
        Assert.Equal(new DateOnly(2026, 5, 18), dto.Days[0].Date);
        Assert.Equal(new DateOnly(2026, 5, 24), dto.Days[6].Date);
    }

    [Fact]
    public async Task GetTimesheetWeek_WeekendCells_NotBusinessDay()
    {
        var userId = await SeedUserAsync(UserRole.User);

        var response = await Client.GetAsync($"/api/timesheet-weeks/{userId}/2026/21");

        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<TimesheetWeekDto>(Json);
        Assert.NotNull(dto);
        // Sat and Sun should not be business days
        Assert.False(dto.Days[5].IsBusinessDay); // Sat
        Assert.False(dto.Days[6].IsBusinessDay); // Sun
        // Mon through Fri should be business days (no holiday seed for in-memory)
        Assert.True(dto.Days[0].IsBusinessDay); // Mon
    }

    [Fact]
    public async Task PutBookings_NewWeek_CreatesAndReturns200()
    {
        var userId = await SeedUserAsync(UserRole.User);
        var customerId = await SeedCustomerAsync();
        var (_, taskId) = await SeedContractWithTaskAsync(userId, customerId);

        var response = await Client.PutAsJsonAsync(
            $"/api/timesheet-weeks/{userId}/2026/21/bookings",
            new { bookings = new[] { new { contractTaskId = taskId, date = "2026-05-18", durationHours = 8.00 } } });

        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<TimesheetWeekDto>(Json);
        Assert.NotNull(dto);
        Assert.NotNull(dto.Id);
        Assert.Single(dto.TimeEntries);
        Assert.Equal(taskId, dto.TimeEntries[0].ContractTaskId);
        Assert.Equal(8.00m, dto.TimeEntries[0].DurationHours);
    }

    [Fact]
    public async Task PutBookings_RoundTrip_AddUpdateRemove()
    {
        var userId = await SeedUserAsync(UserRole.User);
        var customerId = await SeedCustomerAsync();
        var (_, taskId) = await SeedContractWithTaskAsync(userId, customerId);

        // Add
        var r1 = await Client.PutAsJsonAsync(
            $"/api/timesheet-weeks/{userId}/2026/21/bookings",
            new { bookings = new[] { new { contractTaskId = taskId, date = "2026-05-18", durationHours = 8.00 } } });
        r1.EnsureSuccessStatusCode();

        // Update
        var r2 = await Client.PutAsJsonAsync(
            $"/api/timesheet-weeks/{userId}/2026/21/bookings",
            new { bookings = new[] { new { contractTaskId = taskId, date = "2026-05-18", durationHours = 4.00 } } });
        r2.EnsureSuccessStatusCode();
        var dto2 = await r2.Content.ReadFromJsonAsync<TimesheetWeekDto>(Json);
        Assert.Equal(4.00m, dto2!.TimeEntries[0].DurationHours);

        // Remove
        var r3 = await Client.PutAsJsonAsync(
            $"/api/timesheet-weeks/{userId}/2026/21/bookings",
            new { bookings = Array.Empty<object>() });
        r3.EnsureSuccessStatusCode();
        var dto3 = await r3.Content.ReadFromJsonAsync<TimesheetWeekDto>(Json);
        Assert.Empty(dto3!.TimeEntries);
    }

    [Fact]
    public async Task PutBookings_DateOutsideWeek_Returns400()
    {
        var userId = await SeedUserAsync(UserRole.User);
        var customerId = await SeedCustomerAsync();
        var (_, taskId) = await SeedContractWithTaskAsync(userId, customerId);

        var response = await Client.PutAsJsonAsync(
            $"/api/timesheet-weeks/{userId}/2026/21/bookings",
            new { bookings = new[] { new { contractTaskId = taskId, date = "2026-05-25", durationHours = 8.00 } } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Json);
        Assert.Equal(TimesheetErrors.DateOutsideWeek.Code, body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task PutBookings_InvalidDurationHours_Returns400()
    {
        var userId = await SeedUserAsync(UserRole.User);
        var customerId = await SeedCustomerAsync();
        var (_, taskId) = await SeedContractWithTaskAsync(userId, customerId);

        var response = await Client.PutAsJsonAsync(
            $"/api/timesheet-weeks/{userId}/2026/21/bookings",
            new { bookings = new[] { new { contractTaskId = taskId, date = "2026-05-18", durationHours = 0.1 } } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Json);
        Assert.Equal(TimesheetErrors.InvalidDurationHours.Code, body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task PutBookings_InvalidContractTask_Returns400()
    {
        var userId = await SeedUserAsync(UserRole.User);

        var response = await Client.PutAsJsonAsync(
            $"/api/timesheet-weeks/{userId}/2026/21/bookings",
            new { bookings = new[] { new { contractTaskId = Guid.NewGuid(), date = "2026-05-18", durationHours = 8.00 } } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Json);
        Assert.Equal(TimesheetErrors.ContractTaskNotEligible.Code, body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task GetTimesheetWeek_AfterPut_ReturnsPersistedData()
    {
        var userId = await SeedUserAsync(UserRole.User);
        var customerId = await SeedCustomerAsync();
        var (_, taskId) = await SeedContractWithTaskAsync(userId, customerId);

        await Client.PutAsJsonAsync(
            $"/api/timesheet-weeks/{userId}/2026/21/bookings",
            new { bookings = new[] { new { contractTaskId = taskId, date = "2026-05-18", durationHours = 8.00 } } });

        var response = await Client.GetAsync($"/api/timesheet-weeks/{userId}/2026/21");
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<TimesheetWeekDto>(Json);
        Assert.NotNull(dto);
        Assert.NotNull(dto.Id);
        Assert.Single(dto.TimeEntries);
        Assert.Equal(8.00m, dto.TimeEntries[0].DurationHours);
    }

    [Fact]
    public async Task GetSelectableTasks_ReturnsTasksForUser()
    {
        var userId = await SeedUserAsync(UserRole.User);
        var customerId = await SeedCustomerAsync();
        var (_, taskId) = await SeedContractWithTaskAsync(userId, customerId);

        var response = await Client.GetAsync($"/api/timesheet-selectable-tasks/{userId}/2026/21");

        response.EnsureSuccessStatusCode();
        var tasks = await response.Content.ReadFromJsonAsync<SelectableContractTaskDto[]>(Json);
        Assert.NotNull(tasks);
        Assert.Contains(tasks, t => t.ContractTaskId == taskId);
    }

    [Fact]
    public async Task GetSelectableTasks_UserWithNoContracts_ReturnsEmpty()
    {
        var userId = await SeedUserAsync(UserRole.User);

        var response = await Client.GetAsync($"/api/timesheet-selectable-tasks/{userId}/2026/21");

        response.EnsureSuccessStatusCode();
        var tasks = await response.Content.ReadFromJsonAsync<SelectableContractTaskDto[]>(Json);
        Assert.NotNull(tasks);
        Assert.Empty(tasks);
    }
}
