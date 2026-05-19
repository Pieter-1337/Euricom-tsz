using System.Linq.Expressions;
using Moq;
using Shouldly;
using Tsz.Api.Modules.Users;
using Tsz.Api.Modules.Users.Features;
using Tsz.Api.Tests.Builders;
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Tests.Modules.Users.Features;

public class CreateUserHandlerTests
{
    private static (Mock<IUnitOfWork> uow, Mock<IRepository<User>> userRepo, Mock<IRepository<LeaveType>> leaveTypeRepo, Mock<IRepository<UserLeave>> userLeaveRepo)
        BuildMocks(IEnumerable<LeaveType>? leaveTypes = null)
    {
        var userRepo = new Mock<IRepository<User>>();

        var leaveTypeRepo = new Mock<IRepository<LeaveType>>();
        leaveTypeRepo.Setup(r => r.GetAllAsListAsync(null, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(leaveTypes?.ToList() ?? []);

        var userLeaveRepo = new Mock<IRepository<UserLeave>>();

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<User>()).Returns(userRepo.Object);
        uow.Setup(u => u.RepositoryFor<LeaveType>()).Returns(leaveTypeRepo.Object);
        uow.Setup(u => u.RepositoryFor<UserLeave>()).Returns(userLeaveRepo.Object);

        return (uow, userRepo, leaveTypeRepo, userLeaveRepo);
    }

    [Fact]
    public async Task HandleAsync_NoLeaveTypes_AddsUserAndReturnsDto()
    {
        var (uow, userRepo, _, userLeaveRepo) = BuildMocks();
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero));
        var handler = new CreateUserHandler(uow.Object, timeProvider);

        var dto = await handler.HandleAsync(new CreateUserCommand("Jane", "Doe", "jane@example.com", [UserRole.User]));

        dto.ShouldNotBeNull();
        dto.FirstName.ShouldBe("Jane");
        dto.LastName.ShouldBe("Doe");
        dto.Email.ShouldBe("jane@example.com");
        dto.Roles.ShouldBe([UserRole.User]);

        userRepo.Verify(r => r.Add(It.Is<User>(u =>
            u.FirstName == "Jane" && u.LastName == "Doe" && u.Email == "jane@example.com" && u.Roles.Contains(UserRole.User))), Times.Once);
        userLeaveRepo.Verify(r => r.Add(It.IsAny<UserLeave>()), Times.Never);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithLeaveTypes_SeedsOneUserLeaveRowPerLeaveType()
    {
        var fixedDate = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(fixedDate);
        var verlof = LeaveTypeBuilder.Limited("Verlof", 20m);
        var ziekte = LeaveTypeBuilder.Unlimited("Ziekte");
        var (uow, _, _, userLeaveRepo) = BuildMocks(leaveTypes: [verlof, ziekte]);

        var handler = new CreateUserHandler(uow.Object, timeProvider);

        var dto = await handler.HandleAsync(new CreateUserCommand("Jane", "Doe", "jane@example.com", [UserRole.User]));

        dto.ShouldNotBeNull();

        userLeaveRepo.Verify(r => r.Add(It.Is<UserLeave>(ul =>
            ul.LeaveTypeId == verlof.Id &&
            ul.TotalDays == 20m &&
            ul.Year == 2026)), Times.Once);

        userLeaveRepo.Verify(r => r.Add(It.Is<UserLeave>(ul =>
            ul.LeaveTypeId == ziekte.Id &&
            ul.TotalDays == null &&
            ul.Year == 2026)), Times.Once);

        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
