using Microsoft.AspNetCore.Http.HttpResults;
using Tsz.Api.Modules.Users.Features;
using Tsz.Infrastructure.Auth;
using Tsz.Infrastructure.Cqrs;
using Tsz.Infrastructure.Endpoints;
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Modules.Users;

public static class UserEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapApiGroup("users");

        group.MapGet("/me", async (
            IQueryHandler<GetCurrentUserQuery, UserDto?> handler,
            CancellationToken ct) =>
        {
            var user = await handler.HandleAsync(new GetCurrentUserQuery(), ct);
            return user is not null ? Results.Ok(user) : Results.NotFound();
        });

        var adminGroup = group.MapGroup("")
            .RequireAuthorization(AuthorizationPolicies.RequireAdmin);

        adminGroup.MapGet("/", async (
            IQueryHandler<GetUsersQuery, IReadOnlyList<UserDto>> handler,
            CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(new GetUsersQuery(), ct)));

        adminGroup.MapGet("/{id:guid}", async (
            Guid id,
            IQueryHandler<GetUserByIdQuery, UserDto?> handler,
            CancellationToken ct) =>
        {
            var user = await handler.HandleAsync(new GetUserByIdQuery(id), ct);
            return user is not null ? Results.Ok(user) : Results.NotFound();
        }).WithName("GetUserById");

        adminGroup.MapPost("/", async (
            CreateUserCommand command,
            IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            var dto = await dispatcher.SendAsync(command, ct);
            return Results.CreatedAtRoute("GetUserById", new { id = dto.Id }, dto);
        });

        adminGroup.MapPut("/{id:guid}", async (
            Guid id,
            UpdateUserCommand command,
            IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            if (command.Id != id)
                return Results.BadRequest("Route id does not match command id.");

            var dto = await dispatcher.SendAsync(command, ct);
            return Results.Ok(dto);
        });

        adminGroup.MapDelete("/{id:guid}", async (
            Guid id,
            IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            await dispatcher.SendAsync(new DeleteUserCommand(id), ct);
            return Results.NoContent();
        });

        // UserLeave endpoints
        adminGroup.MapGet("/{userId:guid}/leaves", async (
            Guid userId,
            int? year,
            TimeProvider timeProvider,
            IQueryHandler<GetUserLeavesQuery, IReadOnlyList<UserLeaveDto>> handler,
            CancellationToken ct) =>
        {
            var resolvedYear = year ?? timeProvider.GetUtcNow().Year;
            return TypedResults.Ok(await handler.HandleAsync(new GetUserLeavesQuery(userId, resolvedYear), ct));
        });

        adminGroup.MapPut("/{userId:guid}/leaves", async Task<Ok<IReadOnlyList<UserLeaveDto>>> (
            Guid userId,
            UpdateUserLeavesBody body,
            IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            var command = new UpdateUserLeavesCommand(userId, body.Year, body.Items);
            var dtos = await dispatcher.SendAsync(command, ct);
            return TypedResults.Ok(dtos);
        });
    }
}
