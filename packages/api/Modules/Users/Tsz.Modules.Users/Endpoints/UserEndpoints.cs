using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Tsz.Infrastructure.Auth;
using Tsz.Infrastructure.Common.Pagination;
using Tsz.Infrastructure.Cqrs;
using Tsz.Infrastructure.Endpoints;
using Tsz.Modules.Users.Contracts;
using Tsz.Modules.Users.Domain.Users;
using Tsz.Modules.Users.Features;

namespace Tsz.Modules.Users.Endpoints;

public static class UserEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapApiGroup("users");

        group.MapGet("/me", async Task<Results<Ok<UserDto>, NotFound>> (
            IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            var user = await dispatcher.SendAsync(new GetCurrentUserQuery(), ct);
            return user is not null ? TypedResults.Ok(user) : TypedResults.NotFound();
        });

        group.MapGet("/", async (
            IDispatcher dispatcher,
            CancellationToken ct,
            string? role = null) =>
        {
            UserRole? roleFilter = Enum.TryParse<UserRole>(role, ignoreCase: true, out var parsed)
                ? parsed
                : null;
            return TypedResults.Ok(await dispatcher.SendAsync(new GetUsersQuery(roleFilter), ct));
        }).RequireAuthorization(AuthorizationPolicies.RequireAdminOrAnyClientManager);

        var adminGroup = group.MapGroup("")
            .RequireAuthorization(AuthorizationPolicies.RequireAdmin);

        adminGroup.MapGet("/paged", async (
            IDispatcher dispatcher,
            CancellationToken ct,
            string? search = null,
            string? sortBy = null,
            string? sortDir = null,
            int pageSize = 0,
            string? cursor = null,
            bool deletedOnly = false) =>
        {
            var dir = Enum.TryParse<SortDirection>(sortDir, ignoreCase: true, out var parsed)
                ? parsed
                : SortDirection.Asc;
            return TypedResults.Ok(await dispatcher.SendAsync(
                new GetUsersPagedQuery(search, sortBy, dir, pageSize, cursor, deletedOnly), ct));
        });

        adminGroup.MapGet("/impersonation-targets", async (
            IDispatcher dispatcher,
            CancellationToken ct,
            string? search = null,
            string? sortBy = null,
            string? sortDir = null,
            int pageSize = 0,
            string? cursor = null) =>
        {
            var dir = Enum.TryParse<SortDirection>(sortDir, ignoreCase: true, out var parsed)
                ? parsed
                : SortDirection.Asc;
            return TypedResults.Ok(await dispatcher.SendAsync(
                new GetImpersonationTargetsQuery(search, sortBy, dir, pageSize, cursor), ct));
        });

        adminGroup.MapGet("/{id:guid}", async Task<Results<Ok<UserDto>, NotFound>> (
            Guid id,
            IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            var user = await dispatcher.SendAsync(new GetUserByIdQuery(id), ct);
            return user is not null ? TypedResults.Ok(user) : TypedResults.NotFound();
        }).WithName("GetUserById");

        adminGroup.MapPost("/", async Task<CreatedAtRoute<UserDto>> (
            CreateUserCommand command,
            IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            var dto = await dispatcher.SendAsync(command, ct);
            return TypedResults.CreatedAtRoute(dto, "GetUserById", new { id = dto.Id });
        });

        adminGroup.MapPut("/{id:guid}", async Task<Results<Ok<UserDto>, BadRequest<string>>> (
            Guid id,
            UpdateUserCommand command,
            IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            if (command.Id != id)
                return TypedResults.BadRequest("Route id does not match command id.");

            var dto = await dispatcher.SendAsync(command, ct);
            return TypedResults.Ok(dto);
        });

        adminGroup.MapDelete("/{id:guid}", async Task<NoContent> (
            Guid id,
            IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            await dispatcher.SendAsync(new DeleteUserCommand(id), ct);
            return TypedResults.NoContent();
        });
    }
}
