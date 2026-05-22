using Microsoft.AspNetCore.Routing;
using Tsz.Infrastructure.Auth;
using Tsz.Infrastructure.Common.Pagination;
using Tsz.Infrastructure.Cqrs;
using Tsz.Infrastructure.Endpoints;
using Tsz.Modules.Customers.Features;

namespace Tsz.Modules.Customers.Endpoints;

public static class CustomerEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapApiGroup("customers")
            .RequireAuthorization(AuthorizationPolicies.RequireAdminOrAnyClientManager);

        group.MapGet("/", async (IDispatcher dispatcher, CancellationToken ct) =>
            TypedResults.Ok(await dispatcher.SendAsync(new GetCustomersQuery(), ct)));

        group.MapGet("/paged", async (
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
                new GetCustomersPagedQuery(search, sortBy, dir, pageSize, cursor, deletedOnly), ct));
        });

        group.MapGet("/{id:guid}", async (Guid id, IDispatcher dispatcher, CancellationToken ct) =>
        {
            var customer = await dispatcher.SendAsync(new GetCustomerByIdQuery(id), ct);
            return customer is not null ? Results.Ok(customer) : Results.NotFound();
        }).WithName("GetCustomerById");

        group.MapPost("/", async (
            CreateCustomerCommand command,
            IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            var dto = await dispatcher.SendAsync(command, ct);
            return Results.CreatedAtRoute("GetCustomerById", new { id = dto.Id }, dto);
        }).RequireAuthorization(AuthorizationPolicies.RequireAdmin);

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateCustomerCommand command,
            IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            if (command.Id != id)
                return Results.BadRequest("Route id does not match command id.");

            var dto = await dispatcher.SendAsync(command, ct);
            return Results.Ok(dto);
        }).RequireAuthorization(AuthorizationPolicies.RequireAdmin);

        group.MapDelete("/{id:guid}", async (
            Guid id,
            IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            await dispatcher.SendAsync(new DeleteCustomerCommand(id), ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthorizationPolicies.RequireAdmin);
    }
}
