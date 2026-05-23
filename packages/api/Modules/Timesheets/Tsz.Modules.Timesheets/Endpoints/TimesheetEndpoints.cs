using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
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
                userId, year, week, body.Bookings);
            var dto = await dispatcher.SendAsync(command, ct);
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
    }
}

public sealed record ApplyBookingsRequest(IReadOnlyList<BookingInputDto> Bookings);
