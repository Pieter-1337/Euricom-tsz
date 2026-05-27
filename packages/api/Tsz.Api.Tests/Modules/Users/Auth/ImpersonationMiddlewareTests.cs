using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using Tsz.Api.Auth;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Auth;
using Tsz.Modules.Users.Contracts;
using Tsz.Modules.Users.Domain.Users;

namespace Tsz.Api.Tests.Modules.Users.Auth;

public class ImpersonationMiddlewareTests
{
    private static User MakeUser(UserRole role, string? oid = null)
    {
        var user = User.Create("Test", "User", $"user_{Guid.NewGuid():N}@test.com", [role]);
        user.Id = Guid.NewGuid();
        if (oid is not null) user.LinkEntraOid(oid);
        return user;
    }

    private static (HttpContext, ImpersonationContext) BuildContext(
        string? headerValue,
        ResolvedUser? realUser,
        User? targetUser)
    {
        var services = new ServiceCollection();

        var realResolverMock = new Mock<IRealUserResolver>();
        realResolverMock.Setup(r => r.ResolveRealAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(realUser);

        var impersonationContext = new ImpersonationContext();

        var uowMock = new Mock<IUnitOfWork>();
        var repoMock = new Mock<IRepository<User>>();

        if (targetUser is not null)
        {
            repoMock.Setup(r => r.FirstOrDefaultAsync(
                    It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<bool>()))
                .ReturnsAsync((System.Linq.Expressions.Expression<Func<User, bool>> expr, CancellationToken _, bool _) =>
                    expr.Compile()(targetUser) ? targetUser : null);
        }
        else
        {
            repoMock.Setup(r => r.FirstOrDefaultAsync(
                    It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<bool>()))
                .ReturnsAsync((User?)null);
        }

        uowMock.Setup(u => u.RepositoryFor<User>()).Returns(repoMock.Object);

        services.AddSingleton<IRealUserResolver>(realResolverMock.Object);
        services.AddSingleton<IImpersonationContext>(impersonationContext);
        services.AddSingleton<IUnitOfWork>(uowMock.Object);

        var sp = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = sp,
        };
        if (headerValue is not null)
            httpContext.Request.Headers[ImpersonationHeader.Name] = headerValue;

        return (httpContext, impersonationContext);
    }

    [Fact]
    public async Task NoHeader_PassesThrough()
    {
        var (ctx, impCtx) = BuildContext(null, realUser: null, targetUser: null);
        var middleware = new ImpersonationMiddleware(NullLogger<ImpersonationMiddleware>.Instance);
        var nextCalled = false;

        await middleware.InvokeAsync(ctx, _ => { nextCalled = true; return Task.CompletedTask; });

        nextCalled.ShouldBeTrue();
        ((IImpersonationContext)impCtx).IsImpersonating.ShouldBeFalse();
    }

    [Fact]
    public async Task NonAdminCaller_Returns403()
    {
        var nonAdmin = new ResolvedUser(Guid.NewGuid(), [nameof(UserRole.User)]);
        var (ctx, impCtx) = BuildContext(Guid.NewGuid().ToString(), nonAdmin, targetUser: null);
        var middleware = new ImpersonationMiddleware(NullLogger<ImpersonationMiddleware>.Instance);
        var nextCalled = false;

        await middleware.InvokeAsync(ctx, _ => { nextCalled = true; return Task.CompletedTask; });

        ctx.Response.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        nextCalled.ShouldBeFalse();
        ((IImpersonationContext)impCtx).IsImpersonating.ShouldBeFalse();
    }

    [Fact]
    public async Task NoRealUser_Returns403()
    {
        var (ctx, impCtx) = BuildContext(Guid.NewGuid().ToString(), realUser: null, targetUser: null);
        var middleware = new ImpersonationMiddleware(NullLogger<ImpersonationMiddleware>.Instance);
        var nextCalled = false;

        await middleware.InvokeAsync(ctx, _ => { nextCalled = true; return Task.CompletedTask; });

        ctx.Response.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        nextCalled.ShouldBeFalse();
        ((IImpersonationContext)impCtx).IsImpersonating.ShouldBeFalse();
    }

    [Fact]
    public async Task MalformedGuid_Returns400()
    {
        var admin = new ResolvedUser(Guid.NewGuid(), [nameof(UserRole.Admin)]);
        var (ctx, impCtx) = BuildContext("not-a-guid", admin, targetUser: null);
        var middleware = new ImpersonationMiddleware(NullLogger<ImpersonationMiddleware>.Instance);
        var nextCalled = false;

        await middleware.InvokeAsync(ctx, _ => { nextCalled = true; return Task.CompletedTask; });

        ctx.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        nextCalled.ShouldBeFalse();
        ((IImpersonationContext)impCtx).IsImpersonating.ShouldBeFalse();
    }

    [Fact]
    public async Task TargetNotFound_Returns404()
    {
        var admin = new ResolvedUser(Guid.NewGuid(), [nameof(UserRole.Admin)]);
        var (ctx, impCtx) = BuildContext(Guid.NewGuid().ToString(), admin, targetUser: null);
        var middleware = new ImpersonationMiddleware(NullLogger<ImpersonationMiddleware>.Instance);
        var nextCalled = false;

        await middleware.InvokeAsync(ctx, _ => { nextCalled = true; return Task.CompletedTask; });

        ctx.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        nextCalled.ShouldBeFalse();
        ((IImpersonationContext)impCtx).IsImpersonating.ShouldBeFalse();
    }

    [Fact]
    public async Task TargetIsAdmin_Returns403()
    {
        var adminCaller = new ResolvedUser(Guid.NewGuid(), [nameof(UserRole.Admin)]);
        var adminTarget = MakeUser(UserRole.Admin);

        var (ctx, impCtx) = BuildContext(adminTarget.Id.ToString(), adminCaller, adminTarget);
        var middleware = new ImpersonationMiddleware(NullLogger<ImpersonationMiddleware>.Instance);
        var nextCalled = false;

        await middleware.InvokeAsync(ctx, _ => { nextCalled = true; return Task.CompletedTask; });

        ctx.Response.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        nextCalled.ShouldBeFalse();
        ((IImpersonationContext)impCtx).IsImpersonating.ShouldBeFalse();
    }

    [Fact]
    public async Task ValidImpersonation_SetsContextAndCallsNext()
    {
        var adminCaller = new ResolvedUser(Guid.NewGuid(), [nameof(UserRole.Admin)]);
        var plainTarget = MakeUser(UserRole.User);

        var (ctx, impCtx) = BuildContext(plainTarget.Id.ToString(), adminCaller, plainTarget);
        var middleware = new ImpersonationMiddleware(NullLogger<ImpersonationMiddleware>.Instance);
        var nextCalled = false;

        await middleware.InvokeAsync(ctx, _ => { nextCalled = true; return Task.CompletedTask; });

        nextCalled.ShouldBeTrue();
        ((IImpersonationContext)impCtx).IsImpersonating.ShouldBeTrue();
        impCtx.TargetUserId.ShouldBe(plainTarget.Id);
    }
}
