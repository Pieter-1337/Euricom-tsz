using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Tsz.Infrastructure.Auth;
using Tsz.Infrastructure.Common.Pagination;
using Tsz.Infrastructure.Cqrs;
using Tsz.Infrastructure.Endpoints;
using Tsz.Modules.Contracts.Domain.Contracts;
using Tsz.Modules.Contracts.Features;

namespace Tsz.Modules.Contracts.Endpoints;

public static class ContractEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapApiGroup("contracts")
            .RequireAuthorization(AuthorizationPolicies.RequireAdminOrAnyClientManager);

        group.MapGet("/", async (
            IDispatcher dispatcher,
            CancellationToken ct,
            string? search = null,
            string? sortBy = null,
            string? sortDir = null,
            int pageSize = 0,
            string? cursor = null,
            bool deletedOnly = false,
            DateOnly? activeOnDate = null,
            Guid? customerId = null) =>
        {
            var dir = Enum.TryParse<SortDirection>(sortDir, ignoreCase: true, out var parsed)
                ? parsed
                : SortDirection.Asc;
            return TypedResults.Ok(await dispatcher.SendAsync(
                new GetContractsPagedQuery(search, sortBy, dir, pageSize, cursor, deletedOnly, activeOnDate, customerId), ct));
        });

        group.MapGet("/{id:guid}", async (Guid id, IDispatcher dispatcher, CancellationToken ct) =>
        {
            var contract = await dispatcher.SendAsync(new GetContractByIdQuery(id), ct);
            return contract is not null ? Results.Ok(contract) : Results.NotFound();
        }).WithName("GetContractById");

        group.MapPost("/", async Task<Created<ContractDto>> (
            CreateContractCommand command,
            IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            var dto = await dispatcher.SendAsync(command, ct);
            return TypedResults.Created($"/api/contracts/{dto.Id}", dto);
        }).RequireAuthorization(AuthorizationPolicies.RequireAdmin);

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateContractCommand command,
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
            await dispatcher.SendAsync(new DeleteContractCommand(id), ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthorizationPolicies.RequireAdmin);
    }
}
