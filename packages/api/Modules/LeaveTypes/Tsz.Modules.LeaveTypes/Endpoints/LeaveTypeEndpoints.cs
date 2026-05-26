using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Tsz.Infrastructure.Auth;
using Tsz.Infrastructure.Cqrs;
using Tsz.Infrastructure.Endpoints;
using Tsz.Modules.LeaveTypes.Domain.Leaves;
using Tsz.Modules.LeaveTypes.Features;

namespace Tsz.Modules.LeaveTypes.Endpoints;

public static class LeaveTypeEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var usersGroup = app.MapApiGroup("users").MapGroup("");

        usersGroup.MapGet("/{userId:guid}/leaves", async (
            Guid userId,
            int? year,
            TimeProvider timeProvider,
            IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            var resolvedYear = year ?? timeProvider.GetUtcNow().Year;
            return TypedResults.Ok(await dispatcher.SendAsync(new GetUserLeavesQuery(userId, resolvedYear), ct));
        }).RequireAuthorization(AuthorizationPolicies.RequireAdminOrSelf);

        usersGroup.MapPut("/{userId:guid}/leaves", async Task<Ok<IReadOnlyList<UserLeaveDto>>> (
            Guid userId,
            UpdateUserLeavesBody body,
            IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            var command = new UpdateUserLeavesCommand(userId, body.Year, body.Items);
            var dtos = await dispatcher.SendAsync(command, ct);
            return TypedResults.Ok(dtos);
        }).RequireAuthorization(AuthorizationPolicies.RequireAdmin);
    }
}
