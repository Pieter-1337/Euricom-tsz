using Moq;
using Shouldly;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Auth;
using Tsz.Modules.Users.Auth;
using Tsz.Modules.Users.Contracts;
using Tsz.Modules.Users.Domain.Users;

namespace Tsz.Api.Tests.Modules.Users.Auth;

public class CurrentUserResolverTests
{
    private static User MakeUser(UserRole role = UserRole.User)
    {
        var user = User.Create("Test", "User", $"user_{Guid.NewGuid():N}@test.com", [role]);
        return user;
    }

    private static (CurrentUserResolver resolver, RealUserResolver realResolver, ImpersonationContext context, Mock<IUnitOfWork> uow)
        Build(User? realUser, User? targetUser = null, Guid? targetId = null)
    {
        var uowMock = new Mock<IUnitOfWork>();
        var repoMock = new Mock<IRepository<User>>();

        if (realUser is not null)
        {
            repoMock
                .Setup(r => r.FirstOrDefaultAsync(
                    It.Is<System.Linq.Expressions.Expression<Func<User, bool>>>(e =>
                        e.Compile().Invoke(realUser)),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<bool>()))
                .ReturnsAsync(realUser);
        }

        if (targetUser is not null && targetId is not null)
        {
            repoMock
                .Setup(r => r.FirstOrDefaultAsync(
                    It.Is<System.Linq.Expressions.Expression<Func<User, bool>>>(e =>
                        e.Compile().Invoke(targetUser)),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<bool>()))
                .ReturnsAsync(targetUser);
        }

        uowMock.Setup(u => u.RepositoryFor<User>()).Returns(repoMock.Object);

        var currentUserMock = new Mock<ICurrentUser>();
        currentUserMock.Setup(c => c.EntraOid).Returns(realUser?.EntraOid ?? "oid-real");
        currentUserMock.Setup(c => c.Email).Returns(realUser?.Email ?? "real@test.com");

        var realResolver = new RealUserResolver(currentUserMock.Object, uowMock.Object);
        var impersonationContext = new ImpersonationContext();

        var resolver = new CurrentUserResolver(realResolver, impersonationContext, uowMock.Object);

        return (resolver, realResolver, impersonationContext, uowMock);
    }

    [Fact]
    public async Task ResolveAsync_NoImpersonation_ReturnsRealUser()
    {
        var realUser = MakeUser(UserRole.Admin);
        realUser.LinkEntraOid("admin-oid");

        var uowMock = new Mock<IUnitOfWork>();
        var repoMock = new Mock<IRepository<User>>();
        repoMock.Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(realUser);
        uowMock.Setup(u => u.RepositoryFor<User>()).Returns(repoMock.Object);

        var currentUserMock = new Mock<ICurrentUser>();
        currentUserMock.Setup(c => c.EntraOid).Returns("admin-oid");

        var realResolver = new RealUserResolver(currentUserMock.Object, uowMock.Object);
        var impersonation = new ImpersonationContext();
        var resolver = new CurrentUserResolver(realResolver, impersonation, uowMock.Object);

        var result = await ((ICurrentUserResolver)resolver).ResolveAsync();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(realUser.Id);
    }

    [Fact]
    public async Task GetAsync_WithImpersonation_ReturnsTargetUser()
    {
        var realUser = MakeUser(UserRole.Admin);
        var targetUser = MakeUser(UserRole.User);

        var uowMock = new Mock<IUnitOfWork>();
        var repoMock = new Mock<IRepository<User>>();

        repoMock.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
            .ReturnsAsync((System.Linq.Expressions.Expression<Func<User, bool>> expr, CancellationToken _, bool _) =>
            {
                var fn = expr.Compile();
                if (fn(targetUser)) return targetUser;
                if (fn(realUser)) return realUser;
                return null;
            });
        uowMock.Setup(u => u.RepositoryFor<User>()).Returns(repoMock.Object);

        var currentUserMock = new Mock<ICurrentUser>();
        var realResolver = new RealUserResolver(currentUserMock.Object, uowMock.Object);
        var impersonation = new ImpersonationContext();
        impersonation.SetTarget(targetUser.Id);

        var resolver = new CurrentUserResolver(realResolver, impersonation, uowMock.Object);

        var result = await resolver.GetAsync();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(targetUser.Id);
    }

    [Fact]
    public async Task ResolveAsync_WithImpersonation_ReturnsTargetResolvedUser()
    {
        var targetUser = MakeUser(UserRole.User);

        var uowMock = new Mock<IUnitOfWork>();
        var repoMock = new Mock<IRepository<User>>();
        repoMock.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
            .ReturnsAsync(targetUser);
        uowMock.Setup(u => u.RepositoryFor<User>()).Returns(repoMock.Object);

        var currentUserMock = new Mock<ICurrentUser>();
        var realResolver = new RealUserResolver(currentUserMock.Object, uowMock.Object);
        var impersonation = new ImpersonationContext();
        impersonation.SetTarget(targetUser.Id);

        var resolver = new CurrentUserResolver(realResolver, impersonation, uowMock.Object);

        var result = await ((ICurrentUserResolver)resolver).ResolveAsync();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(targetUser.Id);
        result.HasRole(nameof(UserRole.User)).ShouldBeTrue();
        result.HasRole(nameof(UserRole.Admin)).ShouldBeFalse();
    }
}
