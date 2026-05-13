using Tsz.Api.Common.Auth;
using Tsz.Api.Common.Extensions;
using Tsz.Api.Common.Filters;
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
        });

        adminGroup.MapPost("/", async (
            CreateUserCommand command,
            ICommandHandler<CreateUserCommand, CreateUserResult> handler,
            CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(command, ct);
            return result.Conflict
                ? Results.Conflict(new { error = "A user with this email already exists." })
                : Results.Created($"/api/users/{result.User!.Id}", result.User);
        }).AddEndpointFilter<ValidationFilter<CreateUserCommand>>();

        adminGroup.MapPut("/{id:guid}", async (
            Guid id,
            UpdateUserCommand command,
            ICommandHandler<UpdateUserCommand, UserDto?> handler,
            CancellationToken ct) =>
        {
            if (command.Id != id)
                return Results.BadRequest("Route id does not match command id.");

            var user = await handler.HandleAsync(command, ct);
            return user is not null ? Results.Ok(user) : Results.NotFound();
        }).AddEndpointFilter<ValidationFilter<UpdateUserCommand>>();

        adminGroup.MapDelete("/{id:guid}", async (
            Guid id,
            ICommandHandler<DeleteUserCommand, bool> handler,
            CancellationToken ct) =>
                await handler.HandleAsync(new DeleteUserCommand(id), ct)
                    ? Results.NoContent()
                    : Results.NotFound());
    }
}
