using Microsoft.AspNetCore.Authorization;
using Tsz.Infrastructure.Auth;
using Tsz.Modules.Users.Contracts;
using Tsz.Modules.Users.Domain.Users;

namespace Tsz.Modules.Users.Auth;

public sealed class RequireAdminAuthorizationHandler(ICurrentUserResolver resolver)
    : AuthorizationHandler<RequireAdminRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        RequireAdminRequirement requirement)
    {
        var user = await resolver.ResolveAsync();
        if (user is not null && user.Roles.Contains(UserRole.Admin))
            context.Succeed(requirement);
    }
}
