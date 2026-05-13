using Microsoft.AspNetCore.Authorization;
using Tsz.Api.Modules.Users;

namespace Tsz.Api.Common.Auth;

public sealed class RequireAdminAuthorizationHandler(ICurrentUser currentUser)
    : AuthorizationHandler<RequireAdminRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        RequireAdminRequirement requirement)
    {
        if (!currentUser.IsAuthenticated) return;

        var user = await currentUser.GetAsync();
        if (user is { Role: UserRole.Admin })
            context.Succeed(requirement);
    }
}
