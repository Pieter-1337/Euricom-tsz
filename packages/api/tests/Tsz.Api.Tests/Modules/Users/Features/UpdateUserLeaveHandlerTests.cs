using System.Linq.Expressions;
using Moq;
using Shouldly;
using Tsz.Api.Modules.LeaveTypes;
using Tsz.Api.Modules.Users;
using Tsz.Api.Modules.Users.Features;
using Tsz.Api.Tests.Builders;
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Tests.Modules.Users.Features;

public class UpdateUserLeaveHandlerTests
{
    private static (Mock<IUnitOfWork> uow, UserLeave entity, LeaveType lt)
        Setup(LeaveAllowed allowed = LeaveAllowed.Limited, decimal? entityDays = 10m)
    {
        var userId = Guid.NewGuid();
        var leaveTypeId = Guid.NewGuid();
        var lt = LeaveTypeBuilder.Build("Verlof", allowed, allowed == LeaveAllowed.Limited ? 20m : null).WithId(leaveTypeId);
        var entity = UserLeaveBuilder.Build(userId, leaveTypeId, totalDays: entityDays);

        var userLeaveRepo = new Mock<IRepository<UserLeave>>();
        userLeaveRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<UserLeave, bool>>>(), It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(entity);

        var leaveTypeRepo = new Mock<IRepository<LeaveType>>();
        leaveTypeRepo.Setup(r => r.GetByIdAsync(leaveTypeId, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(lt);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<UserLeave>()).Returns(userLeaveRepo.Object);
        uow.Setup(u => u.RepositoryFor<LeaveType>()).Returns(leaveTypeRepo.Object);

        return (uow, entity, lt);
    }

    [Fact]
    public async Task HandleAsync_Limited_UpdatesTotalDays()
    {
        var (uow, entity, _) = Setup(LeaveAllowed.Limited);
        var handler = new UpdateUserLeaveHandler(uow.Object);
        var command = new UpdateUserLeaveCommand(entity.UserId, entity.Id, 42m);

        var result = await handler.HandleAsync(command);

        result.ShouldNotBeNull();
        result.TotalDays.ShouldBe(42m);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
