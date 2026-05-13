using Moq;
using Shouldly;
using Tsz.Api.Modules.Users;
using Tsz.Api.Modules.Users.Features;
using Tsz.Api.Tests.Builders;
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Tests.Modules.Users.Features;

public class UpdateUserHandlerTests
{
    [Fact]
    public async Task HandleAsync_Existing_UpdatesAndReturnsDto()
    {
        var existing = UserBuilder.Build().WithName("Old").WithRole(UserRole.User);
        var repo = new Mock<IRepository<User>>();
        repo.Setup(r => r.GetByIdAsync(existing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<User>()).Returns(repo.Object);

        var handler = new UpdateUserHandler(uow.Object);

        var result = await handler.HandleAsync(new UpdateUserCommand(existing.Id, "New", UserRole.Admin));

        result.ShouldNotBeNull();
        result.Name.ShouldBe("New");
        result.Role.ShouldBe(UserRole.Admin);
        existing.Name.ShouldBe("New");
        existing.Role.ShouldBe(UserRole.Admin);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Missing_ReturnsNull()
    {
        var repo = new Mock<IRepository<User>>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<User>()).Returns(repo.Object);

        var handler = new UpdateUserHandler(uow.Object);

        var result = await handler.HandleAsync(new UpdateUserCommand(Guid.NewGuid(), "x", UserRole.User));

        result.ShouldBeNull();
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
