using Tsz.Api.Modules.LeaveTypes.Features;
using Tsz.Infrastructure.Cqrs;
using Tsz.Infrastructure.Endpoints;
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Modules.LeaveTypes;

public static class LeaveTypeEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapApiGroup("leave-types");

        group.MapGet("/", async (
            IQueryHandler<GetLeaveTypesQuery, IReadOnlyList<LeaveTypeDto>> handler,
            CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(new GetLeaveTypesQuery(), ct)));

        group.MapPost("/", async (
            CreateLeaveTypeCommand command,
            IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            var dto = await dispatcher.SendAsync(command, ct);
            return Results.Created($"/api/leave-types/{dto.Id}", dto);
        });

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateLeaveTypeCommand command,
            IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            if (command.Id != id)
                return Results.BadRequest("Route id does not match command id.");

            var dto = await dispatcher.SendAsync(command, ct);
            return Results.Ok(dto);
        });

        group.MapDelete("/{id:guid}", async (
            Guid id,
            IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            await dispatcher.SendAsync(new DeleteLeaveTypeCommand(id), ct);
            return Results.NoContent();
        });
    }
}
