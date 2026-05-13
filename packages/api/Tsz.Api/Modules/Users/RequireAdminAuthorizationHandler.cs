using Microsoft.AspNetCore.Authorization;
using Tsz.Infrastructure.Auth;

namespace Tsz.Api.Modules.Users;

public sealed class RequireAdminAuthorizationHandler(ICurrentUserResolver resolver)
    : AuthorizationHandler<RequireAdminRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        RequireAdminRequirement requirement)
    {
        var user = await resolver.ResolveAsync();
        if (user is { Role: UserRole.Admin })
            context.Succeed(requirement);
    }
}
