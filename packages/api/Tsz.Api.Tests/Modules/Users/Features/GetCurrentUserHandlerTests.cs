using Moq;
using Shouldly;
using Tsz.Modules.Users.Auth;
using Tsz.Modules.Users.Contracts;
using Tsz.Modules.Users.Domain.Users;
using Tsz.Modules.Users.Features;
using Tsz.Api.Tests.Builders;

namespace Tsz.Api.Tests.Modules.Users.Features;

public class GetCurrentUserHandlerTests
{
    [Fact]
    public async Task HandleAsync_UserFound_ReturnsDto()
    {
        var user = UserBuilder.Build().WithName("Jane", "Doe").WithRoles(UserRole.Admin);
        var resolver = new Mock<ICurrentUserResolver>();
        resolver.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var handler = new GetCurrentUserHandler(resolver.Object);

        var result = await handler.HandleAsync(new GetCurrentUserQuery());

        result.ShouldNotBeNull();
        result!.Id.ShouldBe(user.Id);
        result.FirstName.ShouldBe("Jane");
        result.LastName.ShouldBe("Doe");
        result.Roles.ShouldBe([UserRole.Admin]);
    }

    [Fact]
    public async Task HandleAsync_UserMissing_ReturnsNull()
    {
        var resolver = new Mock<ICurrentUserResolver>();
        resolver.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var handler = new GetCurrentUserHandler(resolver.Object);

        var result = await handler.HandleAsync(new GetCurrentUserQuery());

        result.ShouldBeNull();
    }
}
