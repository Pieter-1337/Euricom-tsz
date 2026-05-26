using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Tsz.Infrastructure.Auth;
using Tsz.Modules.Users.Contracts;

namespace Tsz.Modules.Users.Auth;

public sealed class RequireAdminOrSelfAuthorizationHandler(
    ICurrentUserResolver resolver,
    IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<RequireAdminOrSelfRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        RequireAdminOrSelfRequirement requirement)
    {
        var user = await resolver.ResolveAsync();
        if (user is null) return;

        if (user.HasRole(nameof(UserRole.Admin)))
        {
            context.Succeed(requirement);
            return;
        }

        var routeValue = httpContextAccessor.HttpContext?.GetRouteValue("userId");
        if (routeValue is null) return;

        if (!Guid.TryParse(routeValue.ToString(), out var routeUserId)) return;

        if (user.Id == routeUserId)
            context.Succeed(requirement);
    }
}
