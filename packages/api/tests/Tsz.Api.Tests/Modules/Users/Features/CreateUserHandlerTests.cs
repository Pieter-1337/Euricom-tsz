using System.Linq.Expressions;
using Moq;
using Shouldly;
using Tsz.Api.Modules.LeaveTypes;
using Tsz.Api.Modules.Users;
using Tsz.Api.Modules.Users.Features;
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
            .ReturnsAsync(leaveTypes ?? []);

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

        var handler = new CreateUserHandler(uow.Object);

        var dto = await handler.HandleAsync(new CreateUserCommand("Jane", "jane@example.com", UserRole.User));

        dto.ShouldNotBeNull();
        dto.Name.ShouldBe("Jane");
        dto.Email.ShouldBe("jane@example.com");
        dto.Role.ShouldBe(UserRole.User);

        userRepo.Verify(r => r.Add(It.Is<User>(u =>
            u.Name == "Jane" && u.Email == "jane@example.com" && u.Role == UserRole.User)), Times.Once);
        userLeaveRepo.Verify(r => r.Add(It.IsAny<UserLeave>()), Times.Never);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithLeaveTypes_SeedsOneUserLeaveRowPerLeaveType()
    {
        var verlof = LeaveType.Create("Verlof", LeaveAllowed.Limited, 20m, group: "Verlof");
        var ziekte = LeaveType.Create("Ziekte", LeaveAllowed.Unlimited, null, group: "Illness");
        var (uow, _, _, userLeaveRepo) = BuildMocks(leaveTypes: [verlof, ziekte]);

        var handler = new CreateUserHandler(uow.Object);

        var dto = await handler.HandleAsync(new CreateUserCommand("Jane", "jane@example.com", UserRole.User));

        dto.ShouldNotBeNull();

        userLeaveRepo.Verify(r => r.Add(It.Is<UserLeave>(ul =>
            ul.LeaveTypeId == verlof.Id && ul.TotalDays == 20m)), Times.Once);

        userLeaveRepo.Verify(r => r.Add(It.Is<UserLeave>(ul =>
            ul.LeaveTypeId == ziekte.Id && ul.TotalDays == null)), Times.Once);

        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
