using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Moq;
using Shouldly;
using Tsz.Infrastructure.Auth;
using Tsz.Modules.Users.Auth;
using Tsz.Modules.Users.Contracts;

namespace Tsz.Api.Tests.Modules.Users.Auth;

public class RequireAdminOrSelfAuthorizationHandlerTests
{
    private static RequireAdminOrSelfAuthorizationHandler BuildHandler(
        ResolvedUser? resolvedUser,
        Guid? routeUserId)
    {
        var resolver = new Mock<ICurrentUserResolver>();
        resolver.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(resolvedUser);

        var httpContext = new DefaultHttpContext();
        if (routeUserId.HasValue)
            httpContext.Request.RouteValues["userId"] = routeUserId.Value.ToString();

        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(httpContext);

        return new RequireAdminOrSelfAuthorizationHandler(resolver.Object, accessor.Object);
    }

    private static AuthorizationHandlerContext BuildContext()
    {
        var requirement = new RequireAdminOrSelfRequirement();
        var principal = new ClaimsPrincipal(new ClaimsIdentity("Test"));
        return new AuthorizationHandlerContext([requirement], principal, null);
    }

    [Fact]
    public async Task Admin_AnyUserId_Succeeds()
    {
        var adminId = Guid.NewGuid();
        var admin = new ResolvedUser(adminId, [nameof(UserRole.Admin)]);
        var handler = BuildHandler(admin, Guid.NewGuid());
        var context = BuildContext();

        await handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task NonAdmin_SelfUserId_Succeeds()
    {
        var userId = Guid.NewGuid();
        var user = new ResolvedUser(userId, [nameof(UserRole.User)]);
        var handler = BuildHandler(user, userId);
        var context = BuildContext();

        await handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task NonAdmin_OtherUserId_DoesNotSucceed()
    {
        var userId = Guid.NewGuid();
        var user = new ResolvedUser(userId, [nameof(UserRole.User)]);
        var handler = BuildHandler(user, Guid.NewGuid());
        var context = BuildContext();

        await handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task NoCurrentUser_DoesNotSucceed()
    {
        var handler = BuildHandler(null, Guid.NewGuid());
        var context = BuildContext();

        await handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }
}
