using Microsoft.AspNetCore.Http.HttpResults;
using Tsz.Api.Modules.Users.Features;
using Tsz.Infrastructure.Auth;
using Tsz.Infrastructure.Common.Pagination;
using Tsz.Infrastructure.Cqrs;
using Tsz.Infrastructure.Endpoints;

namespace Tsz.Api.Modules.Users;

public static class UserEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapApiGroup("users");

        group.MapGet("/me", async (IDispatcher dispatcher, CancellationToken ct) =>
        {
            var user = await dispatcher.SendAsync(new GetCurrentUserQuery(), ct);
            return user is not null ? Results.Ok(user) : Results.NotFound();
        });

        var adminGroup = group.MapGroup("")
            .RequireAuthorization(AuthorizationPolicies.RequireAdmin);

        adminGroup.MapGet("/", async (IDispatcher dispatcher, CancellationToken ct) =>
            TypedResults.Ok(await dispatcher.SendAsync(new GetUsersQuery(), ct)));

        adminGroup.MapGet("/paged", async (
            IDispatcher dispatcher,
            CancellationToken ct,
            string? search = null,
            string? sortBy = null,
            string? sortDir = null,
            int pageSize = 0,
            string? cursor = null,
            bool includeDeleted = false) =>
        {
            var dir = Enum.TryParse<SortDirection>(sortDir, ignoreCase: true, out var parsed)
                ? parsed
                : SortDirection.Asc;
            return TypedResults.Ok(await dispatcher.SendAsync(
                new GetUsersPagedQuery(search, sortBy, dir, pageSize, cursor, includeDeleted), ct));
        });

        adminGroup.MapGet("/{id:guid}", async (Guid id, IDispatcher dispatcher, CancellationToken ct) =>
        {
            var user = await dispatcher.SendAsync(new GetUserByIdQuery(id), ct);
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
            IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            var resolvedYear = year ?? timeProvider.GetUtcNow().Year;
            return TypedResults.Ok(await dispatcher.SendAsync(new GetUserLeavesQuery(userId, resolvedYear), ct));
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
