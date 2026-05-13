using Moq;
using Shouldly;
using Tsz.Api.Common.Auth;
using Tsz.Api.Modules.Users;
using Tsz.Api.Tests.Builders;

namespace Tsz.Api.Tests.Modules.Users;

public class GetCurrentUserHandlerTests
{
    [Fact]
    public async Task HandleAsync_UserFound_ReturnsDto()
    {
        var user = UserBuilder.Build().WithName("Jane").WithRole(UserRole.Admin);
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(c => c.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var handler = new GetCurrentUserHandler(currentUser.Object);

        var result = await handler.HandleAsync(new GetCurrentUserQuery());

        result.ShouldNotBeNull();
        result!.Id.ShouldBe(user.Id);
        result.Name.ShouldBe("Jane");
        result.Role.ShouldBe(UserRole.Admin);
    }

    [Fact]
    public async Task HandleAsync_UserMissing_ReturnsNull()
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(c => c.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var handler = new GetCurrentUserHandler(currentUser.Object);

        var result = await handler.HandleAsync(new GetCurrentUserQuery());

        result.ShouldBeNull();
    }
}
