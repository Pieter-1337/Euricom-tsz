using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Tsz.Infrastructure.Auth;
using Tsz.Infrastructure.Common.Pagination;
using Tsz.Infrastructure.Cqrs;
using Tsz.Infrastructure.Endpoints;
using Tsz.Modules.Timesheets.Domain.Holidays;
using Tsz.Modules.Timesheets.Features;

namespace Tsz.Modules.Timesheets.Endpoints;

public static class TimesheetEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapApiGroup("timesheet-weeks")
            .RequireAuthorization();

        group.MapGet("/{userId:guid}/{year:int}/{week:int}", async Task<Results<ForbidHttpResult, Ok<TimesheetWeekDto>>> (
            Guid userId,
            int year,
            int week,
            ICurrentUserResolver currentUserResolver,
            IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            var caller = await currentUserResolver.ResolveAsync(ct);
            if (caller is null || (caller.Id != userId && !caller.HasRole(AuthorizationPolicies.AdminRoleName)))
                return TypedResults.Forbid();

            var dto = await dispatcher.SendAsync(new GetTimesheetWeekQuery(userId, year, week), ct);
            return TypedResults.Ok(dto);
        });

        group.MapPut("/{userId:guid}/{year:int}/{week:int}/bookings", async Task<Results<ForbidHttpResult, Ok<TimesheetWeekDto>>> (
            Guid userId,
            int year,
            int week,
            ApplyBookingsRequest body,
            ICurrentUserResolver currentUserResolver,
            IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            var caller = await currentUserResolver.ResolveAsync(ct);
            if (caller is null || caller.Id != userId)
                return TypedResults.Forbid();

            var command = new ApplyTimesheetWeekBookingsCommand(
                userId, year, week, body.TimeEntries, body.LeaveBookings);
            var dto = await dispatcher.SendAsync(command, ct);
            return TypedResults.Ok(dto);
        });

        group.MapPost("/{userId:guid}/{year:int}/{week:int}/submit", async Task<Results<ForbidHttpResult, Ok<TimesheetWeekDto>>> (
            Guid userId,
            int year,
            int week,
            ICurrentUserResolver currentUserResolver,
            IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            var caller = await currentUserResolver.ResolveAsync(ct);
            if (caller is null || caller.Id != userId)
                return TypedResults.Forbid();

            var dto = await dispatcher.SendAsync(new SubmitTimesheetWeekCommand(userId, year, week), ct);
            return TypedResults.Ok(dto);
        });

        var adminGroup = group.MapGroup("")
            .RequireAuthorization(AuthorizationPolicies.RequireAdmin);

        adminGroup.MapPost("/{userId:guid}/{year:int}/{week:int}/approve", async (
            Guid userId,
            int year,
            int week,
            IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            var dto = await dispatcher.SendAsync(new ApproveTimesheetWeekCommand(userId, year, week), ct);
            return TypedResults.Ok(dto);
        });

        adminGroup.MapPost("/{userId:guid}/{year:int}/{week:int}/reopen", async (
            Guid userId,
            int year,
            int week,
            IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            var dto = await dispatcher.SendAsync(new ReopenTimesheetWeekCommand(userId, year, week), ct);
            return TypedResults.Ok(dto);
        });

        adminGroup.MapGet("/pending-approvals", async Task<Ok<KeysetPage<PendingApprovalDto>>> (
            IDispatcher dispatcher,
            CancellationToken ct,
            string? search = null,
            string? sortBy = null,
            string? sortDir = null,
            int pageSize = 0,
            string? cursor = null,
            bool deletedOnly = false,
            DateOnly? dateFrom = null,
            DateOnly? dateTo = null) =>
        {
            var dir = Enum.TryParse<SortDirection>(sortDir, ignoreCase: true, out var parsed)
                ? parsed
                : SortDirection.Asc;
            var result = await dispatcher.SendAsync(
                new GetPendingApprovalsQuery(search, sortBy, dir, pageSize, cursor, deletedOnly, dateFrom, dateTo), ct);
            return TypedResults.Ok(result);
        });

        var monthGroup = app.MapApiGroup("timesheets")
            .RequireAuthorization();

        monthGroup.MapGet("/{userId:guid}/{year:int}/{month:int}", async Task<Results<ForbidHttpResult, Ok<TimesheetMonthDto>>> (
            Guid userId,
            int year,
            int month,
            ICurrentUserResolver currentUserResolver,
            IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            var caller = await currentUserResolver.ResolveAsync(ct);
            if (caller is null || (caller.Id != userId && !caller.HasRole(AuthorizationPolicies.AdminRoleName)))
                return TypedResults.Forbid();

            var dto = await dispatcher.SendAsync(new GetTimesheetMonthQuery(userId, year, month), ct);
            return TypedResults.Ok(dto);
        });

        var tasksGroup = app.MapApiGroup("timesheet-selectable-tasks")
            .RequireAuthorization();

        tasksGroup.MapGet("/{userId:guid}/{year:int}/{week:int}", async (
            Guid userId,
            int year,
            int week,
            IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            var tasks = await dispatcher.SendAsync(
                new GetSelectableContractTasksQuery(userId, year, week), ct);
            return TypedResults.Ok(tasks);
        });

        var leaveTypesGroup = app.MapApiGroup("timesheet-selectable-leave-types")
            .RequireAuthorization();

        leaveTypesGroup.MapGet("/{userId:guid}", async (
            Guid userId,
            IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            var leaveTypes = await dispatcher.SendAsync(
                new GetSelectableLeaveTypesQuery(userId), ct);
            return TypedResults.Ok(leaveTypes);
        });

        group.MapGet("/{userId:guid}/leave-bookings", async (
            Guid userId,
            int year,
            IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            var dto = await dispatcher.SendAsync(new GetLeaveBookingsForYearQuery(userId, year), ct);
            return TypedResults.Ok(dto);
        }).RequireAuthorization(AuthorizationPolicies.RequireAdminOrSelf);

        var workdaysGroup = app.MapApiGroup("workdays")
            .RequireAuthorization();

        workdaysGroup.MapGet("/holidays", async (
            int year,
            IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            var dto = await dispatcher.SendAsync(new GetHolidaysInYearQuery(year), ct);
            return TypedResults.Ok(dto);
        });
    }
}

public sealed record ApplyBookingsRequest(
    IReadOnlyList<TimeEntryInputDto> TimeEntries,
    IReadOnlyList<LeaveBookingInputDto> LeaveBookings);
