using System.Net;
using System.Net.Http.Json;
using Tsz.Api.Tests.Integration.TestAuth;
using Tsz.Modules.Contracts.Contracts.Queries;
using Tsz.Modules.Contracts.Domain.Contracts;
using Tsz.Modules.Customers.Domain.Customers;
using Tsz.Modules.LeaveTypes.Domain.LeaveTypes;
using Tsz.Modules.LeaveTypes.Domain.Leaves;
using Tsz.Modules.Timesheets.Domain.Timesheets;
using Tsz.Modules.Timesheets.Features;
using Tsz.Modules.Users.Contracts;
using Tsz.Modules.Users.Domain.Users;

namespace Tsz.Api.Tests.Integration;

public class TimesheetEndpointsTests : IntegrationTestBase, IAsyncLifetime
{
    private const string TestOid = "test-user-oid";

    public TimesheetEndpointsTests(TestWebApplicationFactory factory) : base(factory) { }

    // Seeded leave type IDs (from LeaveTypeConfiguration.HasData)
    private static readonly Guid VerlofId = new("11111111-1111-1111-1111-000000000001");

    public async Task InitializeAsync()
    {
        await WithUowAsync(uow => uow.RepositoryFor<TimesheetWeek>().BatchHardDeleteAsync(_ => true));
        await WithUowAsync(uow => uow.RepositoryFor<UserLeave>().BatchHardDeleteAsync(_ => true));
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
            new { timeEntries = new[] { new { contractTaskId = taskId, date = "2026-05-18", durationHours = 8.00 } }, leaveBookings = Array.Empty<object>() });

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
            new { timeEntries = new[] { new { contractTaskId = taskId, date = "2026-05-18", durationHours = 8.00 } }, leaveBookings = Array.Empty<object>() });
        r1.EnsureSuccessStatusCode();

        // Update
        var r2 = await Client.PutAsJsonAsync(
            $"/api/timesheet-weeks/{userId}/2026/21/bookings",
            new { timeEntries = new[] { new { contractTaskId = taskId, date = "2026-05-18", durationHours = 4.00 } }, leaveBookings = Array.Empty<object>() });
        r2.EnsureSuccessStatusCode();
        var dto2 = await r2.Content.ReadFromJsonAsync<TimesheetWeekDto>(Json);
        Assert.Equal(4.00m, dto2!.TimeEntries[0].DurationHours);

        // Remove
        var r3 = await Client.PutAsJsonAsync(
            $"/api/timesheet-weeks/{userId}/2026/21/bookings",
            new { timeEntries = Array.Empty<object>(), leaveBookings = Array.Empty<object>() });
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
            new { timeEntries = new[] { new { contractTaskId = taskId, date = "2026-05-25", durationHours = 8.00 } }, leaveBookings = Array.Empty<object>() });

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
            new { timeEntries = new[] { new { contractTaskId = taskId, date = "2026-05-18", durationHours = 0.1 } }, leaveBookings = Array.Empty<object>() });

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
            new { timeEntries = new[] { new { contractTaskId = Guid.NewGuid(), date = "2026-05-18", durationHours = 8.00 } }, leaveBookings = Array.Empty<object>() });

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
            new { timeEntries = new[] { new { contractTaskId = taskId, date = "2026-05-18", durationHours = 8.00 } }, leaveBookings = Array.Empty<object>() });

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

    // --- Lifecycle helpers (use oid matching TestAuthHandler) ---

    private const string CallerOid = "test-user-id";

    private async Task<Guid> SeedCallerAsAsync(UserRole role)
    {
        var id = Guid.NewGuid();
        await WithUowAsync(async uow =>
        {
            var user = User.Create("Caller", "User", $"caller-{id}@test.com", [role]);
            user.Id = id;
            user.LinkEntraOid(CallerOid);
            uow.RepositoryFor<User>().Add(user);
            await uow.SaveChangesAsync();
        });
        return id;
    }

    private async Task<TimesheetWeek> SeedDraftWeekAsync(Guid userId)
    {
        TimesheetWeek result = null!;
        await WithUowAsync(async uow =>
        {
            var week = TimesheetWeek.Create(userId, 2026, 22);
            uow.RepositoryFor<TimesheetWeek>().Add(week);
            await uow.SaveChangesAsync();
            result = week;
        });
        return result;
    }

    // --- Submit ---

    [Fact]
    public async Task Submit_AsOwner_Draft_Returns200Submitted()
    {
        var userId = await SeedCallerAsAsync(UserRole.User);
        await SeedDraftWeekAsync(userId);

        var response = await Client.PostAsync($"/api/timesheet-weeks/{userId}/2026/22/submit", null);

        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<TimesheetWeekDto>(Json);
        Assert.NotNull(dto);
        Assert.Equal(nameof(TimesheetStatus.Submitted), dto.Status);
    }

    [Fact]
    public async Task Submit_AsOtherUser_Returns403()
    {
        var otherUserId = Guid.NewGuid();
        await SeedCallerAsAsync(UserRole.User); // seeds caller with CallerOid, but request targets otherUserId
        await WithUowAsync(async uow =>
        {
            var other = User.Create("Other", "User", "other@test.com", [UserRole.User]);
            other.Id = otherUserId;
            uow.RepositoryFor<User>().Add(other);
            await uow.SaveChangesAsync();
        });
        await SeedDraftWeekAsync(otherUserId);

        var response = await Client.PostAsync($"/api/timesheet-weeks/{otherUserId}/2026/22/submit", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Submit_AlreadySubmitted_Returns400()
    {
        var userId = await SeedCallerAsAsync(UserRole.User);
        await SeedDraftWeekAsync(userId);
        await Client.PostAsync($"/api/timesheet-weeks/{userId}/2026/22/submit", null);

        var response = await Client.PostAsync($"/api/timesheet-weeks/{userId}/2026/22/submit", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Json);
        Assert.Equal(TimesheetErrors.NotSubmittable.Code, body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Submit_NoDraftRow_Returns404NotFound()
    {
        var userId = await SeedCallerAsAsync(UserRole.User);

        var response = await Client.PostAsync($"/api/timesheet-weeks/{userId}/2026/22/submit", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Json);
        Assert.Equal(TimesheetErrors.NotFound.Code, body.GetProperty("code").GetString());
    }

    // --- Approve ---

    [Fact]
    public async Task Approve_AsAdmin_Submitted_Returns200Approved()
    {
        var adminId = await SeedCallerAsAsync(UserRole.Admin);
        await SeedDraftWeekAsync(adminId);
        await Client.PostAsync($"/api/timesheet-weeks/{adminId}/2026/22/submit", null);

        var response = await Client.PostAsync($"/api/timesheet-weeks/{adminId}/2026/22/approve", null);

        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<TimesheetWeekDto>(Json);
        Assert.NotNull(dto);
        Assert.Equal(nameof(TimesheetStatus.Approved), dto.Status);
    }

    [Fact]
    public async Task Approve_AsNonAdmin_Returns403()
    {
        var userId = await SeedCallerAsAsync(UserRole.User);
        await SeedDraftWeekAsync(userId);
        await Client.PostAsync($"/api/timesheet-weeks/{userId}/2026/22/submit", null);

        var response = await Client.PostAsync($"/api/timesheet-weeks/{userId}/2026/22/approve", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Approve_StillDraft_Returns400()
    {
        var adminId = await SeedCallerAsAsync(UserRole.Admin);
        await SeedDraftWeekAsync(adminId);

        var response = await Client.PostAsync($"/api/timesheet-weeks/{adminId}/2026/22/approve", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Json);
        Assert.Equal(TimesheetErrors.NotApprovable.Code, body.GetProperty("code").GetString());
    }

    // --- Reopen ---

    [Fact]
    public async Task Reopen_AsAdmin_FromApproved_Returns200Draft()
    {
        var adminId = await SeedCallerAsAsync(UserRole.Admin);
        await SeedDraftWeekAsync(adminId);
        await Client.PostAsync($"/api/timesheet-weeks/{adminId}/2026/22/submit", null);
        await Client.PostAsync($"/api/timesheet-weeks/{adminId}/2026/22/approve", null);

        var response = await Client.PostAsync($"/api/timesheet-weeks/{adminId}/2026/22/reopen", null);

        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<TimesheetWeekDto>(Json);
        Assert.NotNull(dto);
        Assert.Equal(nameof(TimesheetStatus.Draft), dto.Status);
    }

    [Fact]
    public async Task Reopen_AsAdmin_FromSubmitted_Returns200Draft()
    {
        var adminId = await SeedCallerAsAsync(UserRole.Admin);
        await SeedDraftWeekAsync(adminId);
        await Client.PostAsync($"/api/timesheet-weeks/{adminId}/2026/22/submit", null);

        var response = await Client.PostAsync($"/api/timesheet-weeks/{adminId}/2026/22/reopen", null);

        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<TimesheetWeekDto>(Json);
        Assert.NotNull(dto);
        Assert.Equal(nameof(TimesheetStatus.Draft), dto.Status);
    }

    [Fact]
    public async Task Reopen_AsNonAdmin_Returns403()
    {
        var userId = await SeedCallerAsAsync(UserRole.User);
        await SeedDraftWeekAsync(userId);

        var response = await Client.PostAsync($"/api/timesheet-weeks/{userId}/2026/22/reopen", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Reopen_StillDraft_Returns400()
    {
        var adminId = await SeedCallerAsAsync(UserRole.Admin);
        await SeedDraftWeekAsync(adminId);

        var response = await Client.PostAsync($"/api/timesheet-weeks/{adminId}/2026/22/reopen", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Json);
        Assert.Equal(TimesheetErrors.NotReopenable.Code, body.GetProperty("code").GetString());
    }

    // --- Lock-while-non-Draft ---

    [Fact]
    public async Task PutBookings_WhenApproved_Returns400NotDraft()
    {
        var adminId = await SeedCallerAsAsync(UserRole.Admin);
        var customerId = await SeedCustomerAsync();
        var (_, taskId) = await SeedContractWithTaskAsync(adminId, customerId);
        await SeedDraftWeekAsync(adminId);
        await Client.PostAsync($"/api/timesheet-weeks/{adminId}/2026/22/submit", null);
        await Client.PostAsync($"/api/timesheet-weeks/{adminId}/2026/22/approve", null);

        var response = await Client.PutAsJsonAsync(
            $"/api/timesheet-weeks/{adminId}/2026/22/bookings",
            new { timeEntries = new[] { new { contractTaskId = taskId, date = "2026-05-25", durationHours = 8.00 } }, leaveBookings = Array.Empty<object>() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Json);
        Assert.Equal(TimesheetErrors.NotDraft.Code, body.GetProperty("code").GetString());
    }

    // --- Leave booking integration tests ---

    private async Task SeedUserLeaveAsync(Guid userId, Guid leaveTypeId, decimal? totalDays)
    {
        await WithUowAsync(async uow =>
        {
            var userLeave = UserLeave.Create(userId, leaveTypeId, 2026, totalDays);
            uow.RepositoryFor<UserLeave>().Add(userLeave);
            await uow.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task PutLeaveBookings_UnderAllowance_Returns200()
    {
        var userId = await SeedUserAsync(UserRole.User);
        await SeedUserLeaveAsync(userId, VerlofId, 5m); // 5 days = 40h

        var response = await Client.PutAsJsonAsync(
            $"/api/timesheet-weeks/{userId}/2026/21/bookings",
            new
            {
                timeEntries = Array.Empty<object>(),
                leaveBookings = new[] { new { leaveTypeId = VerlofId, date = "2026-05-18", durationHours = 8.00 } }
            });

        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<TimesheetWeekDto>(Json);
        Assert.NotNull(dto);
        Assert.Single(dto.LeaveBookings);
        Assert.Equal(VerlofId, dto.LeaveBookings[0].LeaveTypeId);
        Assert.Equal(8.00m, dto.LeaveBookings[0].DurationHours);
    }

    [Fact]
    public async Task PutLeaveBookings_OverAllowance_Returns400LeaveAllowanceExceeded()
    {
        var userId = await SeedUserAsync(UserRole.User);
        await SeedUserLeaveAsync(userId, VerlofId, 1m); // 1 day = 8h

        var response = await Client.PutAsJsonAsync(
            $"/api/timesheet-weeks/{userId}/2026/21/bookings",
            new
            {
                timeEntries = Array.Empty<object>(),
                leaveBookings = new[]
                {
                    new { leaveTypeId = VerlofId, date = "2026-05-18", durationHours = 8.00 },
                    new { leaveTypeId = VerlofId, date = "2026-05-19", durationHours = 4.00 }
                }
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Json);
        Assert.Equal(TimesheetErrors.LeaveAllowanceExceeded.Code, body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task PutLeaveBookings_NullAllowance_Unlimited_Returns200()
    {
        var userId = await SeedUserAsync(UserRole.User);
        await SeedUserLeaveAsync(userId, VerlofId, null); // unlimited

        var response = await Client.PutAsJsonAsync(
            $"/api/timesheet-weeks/{userId}/2026/21/bookings",
            new
            {
                timeEntries = Array.Empty<object>(),
                leaveBookings = new[]
                {
                    new { leaveTypeId = VerlofId, date = "2026-05-18", durationHours = 8.00 },
                    new { leaveTypeId = VerlofId, date = "2026-05-19", durationHours = 8.00 },
                    new { leaveTypeId = VerlofId, date = "2026-05-20", durationHours = 8.00 },
                    new { leaveTypeId = VerlofId, date = "2026-05-21", durationHours = 8.00 },
                    new { leaveTypeId = VerlofId, date = "2026-05-22", durationHours = 8.00 }
                }
            });

        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<TimesheetWeekDto>(Json);
        Assert.NotNull(dto);
        Assert.Equal(5, dto.LeaveBookings.Count);
    }

    [Fact]
    public async Task PutLeaveBookings_MixedWithTimeEntries_RoundTrips()
    {
        var userId = await SeedUserAsync(UserRole.User);
        var customerId = await SeedCustomerAsync();
        var (_, taskId) = await SeedContractWithTaskAsync(userId, customerId);
        await SeedUserLeaveAsync(userId, VerlofId, 5m);

        var response = await Client.PutAsJsonAsync(
            $"/api/timesheet-weeks/{userId}/2026/21/bookings",
            new
            {
                timeEntries = new[] { new { contractTaskId = taskId, date = "2026-05-18", durationHours = 8.00 } },
                leaveBookings = new[] { new { leaveTypeId = VerlofId, date = "2026-05-19", durationHours = 4.00 } }
            });

        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<TimesheetWeekDto>(Json);
        Assert.NotNull(dto);
        Assert.Single(dto.TimeEntries);
        Assert.Single(dto.LeaveBookings);
        Assert.Equal(8.00m, dto.TimeEntries[0].DurationHours);
        Assert.Equal(4.00m, dto.LeaveBookings[0].DurationHours);
    }

    [Fact]
    public async Task GetSelectableLeaveTypes_ReturnsActiveLeaveTypes()
    {
        var userId = await SeedUserAsync(UserRole.User);

        var response = await Client.GetAsync($"/api/timesheet-selectable-leave-types/{userId}");

        response.EnsureSuccessStatusCode();
        var leaveTypes = await response.Content.ReadFromJsonAsync<SelectableLeaveTypeDto[]>(Json);
        Assert.NotNull(leaveTypes);
        Assert.NotEmpty(leaveTypes);
    }
}
