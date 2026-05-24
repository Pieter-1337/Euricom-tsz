using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Tsz.Infrastructure.Auth;
using Tsz.Infrastructure.Cqrs;
using Tsz.Infrastructure.Endpoints;
using Tsz.Modules.Timesheets.Features;

namespace Tsz.Modules.Timesheets.Endpoints;

public static class TimesheetEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapApiGroup("timesheet-weeks")
            .RequireAuthorization();

        group.MapGet("/{userId:guid}/{year:int}/{week:int}", async (
            Guid userId,
            int year,
            int week,
            IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            var dto = await dispatcher.SendAsync(new GetTimesheetWeekQuery(userId, year, week), ct);
            return TypedResults.Ok(dto);
        });

        group.MapPut("/{userId:guid}/{year:int}/{week:int}/bookings", async (
            Guid userId,
            int year,
            int week,
            ApplyBookingsRequest body,
            IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            var command = new ApplyTimesheetWeekBookingsCommand(
                userId, year, week, body.TimeEntries, body.LeaveBookings);
            var dto = await dispatcher.SendAsync(command, ct);
            return TypedResults.Ok(dto);
        });

        group.MapPost("/{userId:guid}/{year:int}/{week:int}/submit", async (
            Guid userId,
            int year,
            int week,
            ICurrentUserResolver currentUserResolver,
            IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            var caller = await currentUserResolver.ResolveAsync(ct);
            if (caller is null || caller.Id != userId)
                return Results.Forbid();

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

        var monthGroup = app.MapApiGroup("timesheets")
            .RequireAuthorization();

        monthGroup.MapGet("/{userId:guid}/{year:int}/{month:int}", async (
            Guid userId,
            int year,
            int month,
            ICurrentUserResolver currentUserResolver,
            IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            var caller = await currentUserResolver.ResolveAsync(ct);
            if (caller is null || (caller.Id != userId && !caller.HasRole(AuthorizationPolicies.AdminRoleName)))
                return Results.Forbid();

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
    }
}

public sealed record ApplyBookingsRequest(
    IReadOnlyList<TimeEntryInputDto> TimeEntries,
    IReadOnlyList<LeaveBookingInputDto> LeaveBookings);
