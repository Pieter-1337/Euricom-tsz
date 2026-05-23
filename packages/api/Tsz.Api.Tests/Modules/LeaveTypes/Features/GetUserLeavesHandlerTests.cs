using System.Linq.Expressions;
using Moq;
using Shouldly;
using Tsz.Modules.LeaveTypes.Contracts;
using Tsz.Modules.LeaveTypes.Domain.LeaveTypes;
using Tsz.Modules.LeaveTypes.Domain.Leaves;
using Tsz.Modules.LeaveTypes.Features;
using Tsz.Api.Tests.Builders;
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Tests.Modules.LeaveTypes.Features;

public class GetUserLeavesHandlerTests
{
    private static (Mock<IUnitOfWork> uow, Mock<IRepository<UserLeave>> userLeaveRepo, Mock<IRepository<LeaveType>> leaveTypeRepo)
        BuildMocks(IEnumerable<UserLeave> leaves, IEnumerable<LeaveType> leaveTypes)
    {
        var userLeaveRepo = new Mock<IRepository<UserLeave>>();
        userLeaveRepo.Setup(r => r.GetAllAsListAsync(
                It.IsAny<Expression<Func<UserLeave, bool>>>(),
                It.IsAny<CancellationToken>(),
                false))
            .ReturnsAsync(leaves.ToList());

        var leaveTypeRepo = new Mock<IRepository<LeaveType>>();
        leaveTypeRepo.Setup(r => r.GetAllAsListAsync(
                It.IsAny<Expression<Func<LeaveType, bool>>>(),
                It.IsAny<CancellationToken>(),
                false))
            .ReturnsAsync(leaveTypes.ToList());
        leaveTypeRepo.Setup(r => r.GetAllAsListAsync(null, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(leaveTypes.ToList());

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<UserLeave>()).Returns(userLeaveRepo.Object);
        uow.Setup(u => u.RepositoryFor<LeaveType>()).Returns(leaveTypeRepo.Object);

        return (uow, userLeaveRepo, leaveTypeRepo);
    }

    [Fact]
    public async Task HandleAsync_WithRows_ReturnsDtosJoinedWithLeaveType()
    {
        var userId = Guid.NewGuid();
        var leaveTypeId = Guid.NewGuid();
        var leaveType = LeaveTypeBuilder.Limited("Verlof", 20m).WithId(leaveTypeId);
        var year = 2026;
        var leave = UserLeaveBuilder.Build(userId, leaveTypeId, year, 20m);

        var (uow, _, _) = BuildMocks([leave], [leaveType]);
        var handler = new GetUserLeavesHandler(uow.Object);

        var result = await handler.HandleAsync(new GetUserLeavesQuery(userId, year));

        result.Count.ShouldBe(1);
        result[0].LeaveTypeName.ShouldBe("Verlof");
        result[0].DefaultAllowed.ShouldBe(LeaveAllowed.Limited);
        result[0].Year.ShouldBe(year);
        result[0].TotalDays.ShouldBe(20m);
        result[0].TakenDays.ShouldBeNull();
        result[0].BalanceDays.ShouldBeNull();
    }

    [Fact]
    public async Task HandleAsync_NoRowsForYear_ReturnsEmpty()
    {
        var userId = Guid.NewGuid();

        var (uow, _, _) = BuildMocks([], []);
        var handler = new GetUserLeavesHandler(uow.Object);

        var result = await handler.HandleAsync(new GetUserLeavesQuery(userId, 2020));

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_MultipleLeaveTypes_ReturnsAllJoined()
    {
        var userId = Guid.NewGuid();
        var ltVerlof = LeaveTypeBuilder.Limited("Verlof", 20m);
        var ltZiekte = LeaveTypeBuilder.Unlimited("Ziekte");
        var year = 2026;

        var leaveVerlof = UserLeaveBuilder.Build(userId, ltVerlof.Id, year, 20m);
        var leaveZiekte = UserLeaveBuilder.Build(userId, ltZiekte.Id, year, null);

        var (uow, _, _) = BuildMocks([leaveVerlof, leaveZiekte], [ltVerlof, ltZiekte]);
        var handler = new GetUserLeavesHandler(uow.Object);

        var result = await handler.HandleAsync(new GetUserLeavesQuery(userId, year));

        result.Count.ShouldBe(2);
        result.ShouldContain(r => r.LeaveTypeName == "Verlof" && r.DefaultAllowed == LeaveAllowed.Limited);
        result.ShouldContain(r => r.LeaveTypeName == "Ziekte" && r.DefaultAllowed == LeaveAllowed.Unlimited);
    }
}
