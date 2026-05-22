using Microsoft.AspNetCore.Authorization;
using Tsz.Infrastructure.Auth;
using Tsz.Modules.Users.Contracts;

namespace Tsz.Modules.Users.Auth;

public sealed class RequireAdminOrAnyClientManagerAuthorizationHandler(ICurrentUserResolver resolver)
    : AuthorizationHandler<RequireAdminOrAnyClientManagerRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        RequireAdminOrAnyClientManagerRequirement requirement)
    {
        var user = await resolver.ResolveAsync();
        if (user is null) return;
        if (user.HasRole(nameof(UserRole.Admin)) || user.HasRole(nameof(UserRole.ClientManager)))
            context.Succeed(requirement);
    }
}
