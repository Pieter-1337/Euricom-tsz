using System.Linq.Expressions;
using Moq;
using Shouldly;
using Tsz.Api.Modules.LeaveTypes;
using Tsz.Api.Modules.Users;
using Tsz.Api.Modules.Users.Features;
using Tsz.Api.Tests.Builders;
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Tests.Modules.Users.Features;

public class GetUserLeavesHandlerTests
{
    [Fact]
    public async Task HandleAsync_FiltersOnUserId_ReturnsDtosWithJoinedLeaveType()
    {
        var userId = Guid.NewGuid();
        var leaveTypeId = Guid.NewGuid();
        var leaveType = LeaveTypeBuilder.Build("Verlof", LeaveAllowed.Limited, 20m).WithId(leaveTypeId);

        var leave = UserLeaveBuilder.Build(userId, leaveTypeId, totalDays: 20m);

        var userLeaveRepo = new Mock<IRepository<UserLeave>>();
        userLeaveRepo.Setup(r => r.GetAllAsListAsync(
                It.IsAny<Expression<Func<UserLeave, bool>>>(),
                It.IsAny<CancellationToken>(),
                false))
            .ReturnsAsync([leave]);

        var leaveTypeRepo = new Mock<IRepository<LeaveType>>();
        leaveTypeRepo.Setup(r => r.GetAllAsListAsync(null, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync([leaveType]);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<UserLeave>()).Returns(userLeaveRepo.Object);
        uow.Setup(u => u.RepositoryFor<LeaveType>()).Returns(leaveTypeRepo.Object);

        var handler = new GetUserLeavesHandler(uow.Object);

        var result = await handler.HandleAsync(new GetUserLeavesQuery(userId));

        result.Count.ShouldBe(1);
        result[0].LeaveTypeName.ShouldBe("Verlof");
        result[0].DefaultAllowed.ShouldBe(LeaveAllowed.Limited);
        result[0].TotalDays.ShouldBe(20m);
    }

    [Fact]
    public async Task HandleAsync_MissingRow_MaterializesFromLeaveType()
    {
        var userId = Guid.NewGuid();
        var leaveType = LeaveTypeBuilder.Build("Ziekte", LeaveAllowed.Unlimited, null);

        var userLeaveRepo = new Mock<IRepository<UserLeave>>();
        userLeaveRepo.Setup(r => r.GetAllAsListAsync(
                It.IsAny<Expression<Func<UserLeave, bool>>>(),
                It.IsAny<CancellationToken>(),
                false))
            .ReturnsAsync([]);

        var leaveTypeRepo = new Mock<IRepository<LeaveType>>();
        leaveTypeRepo.Setup(r => r.GetAllAsListAsync(null, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync([leaveType]);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<UserLeave>()).Returns(userLeaveRepo.Object);
        uow.Setup(u => u.RepositoryFor<LeaveType>()).Returns(leaveTypeRepo.Object);

        var handler = new GetUserLeavesHandler(uow.Object);

        var result = await handler.HandleAsync(new GetUserLeavesQuery(userId));

        result.Count.ShouldBe(1);
        result[0].LeaveTypeName.ShouldBe("Ziekte");
        result[0].DefaultAllowed.ShouldBe(LeaveAllowed.Unlimited);
        result[0].TotalDays.ShouldBeNull();
        userLeaveRepo.Verify(r => r.Add(It.IsAny<UserLeave>()), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
