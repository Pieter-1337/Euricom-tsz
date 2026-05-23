using Moq;
using Shouldly;
using Tsz.Modules.LeaveTypes.Contracts;
using Tsz.Modules.Users.Contracts;
using Tsz.Modules.Users.Domain.Users;
using Tsz.Modules.Users.Features;
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Tests.Modules.Users.Features;

public class CreateUserHandlerTests
{
    private static (Mock<IUnitOfWork> uow, Mock<IRepository<User>> userRepo, Mock<ILeaveTypesAccessModule> leaveTypesFacade)
        BuildMocks()
    {
        var userRepo = new Mock<IRepository<User>>();

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<User>()).Returns(userRepo.Object);

        var leaveTypesFacade = new Mock<ILeaveTypesAccessModule>();
        leaveTypesFacade
            .Setup(m => m.SeedUserLeavesAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return (uow, userRepo, leaveTypesFacade);
    }

    [Fact]
    public async Task HandleAsync_CreatesUserAndSeedsLeaves()
    {
        var (uow, userRepo, leaveTypesFacade) = BuildMocks();
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero));
        var handler = new CreateUserHandler(uow.Object, timeProvider, leaveTypesFacade.Object);

        var dto = await handler.HandleAsync(new CreateUserCommand("Jane", "Doe", "jane@example.com", [UserRole.User]));

        dto.ShouldNotBeNull();
        dto.FirstName.ShouldBe("Jane");
        dto.LastName.ShouldBe("Doe");
        dto.Email.ShouldBe("jane@example.com");
        dto.Roles.ShouldBe([UserRole.User]);

        userRepo.Verify(r => r.Add(It.Is<User>(u =>
            u.FirstName == "Jane" && u.LastName == "Doe" && u.Email == "jane@example.com" && u.Roles.Contains(UserRole.User))), Times.Once);
        leaveTypesFacade.Verify(m => m.SeedUserLeavesAsync(
            It.IsAny<Guid>(), 2026, It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_SeedCalledBeforeSave_SingleTransaction()
    {
        var callOrder = new List<string>();
        var (uow, userRepo, leaveTypesFacade) = BuildMocks();
        leaveTypesFacade
            .Setup(m => m.SeedUserLeavesAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("seed"))
            .Returns(Task.CompletedTask);
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("save"))
            .ReturnsAsync(0);

        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero));
        var handler = new CreateUserHandler(uow.Object, timeProvider, leaveTypesFacade.Object);

        await handler.HandleAsync(new CreateUserCommand("Jane", "Doe", "jane@example.com", [UserRole.User]));

        callOrder.ShouldBe(["seed", "save"]);
    }

    private sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
