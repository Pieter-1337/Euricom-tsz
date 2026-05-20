using System.Linq.Expressions;
using Moq;
using Shouldly;
using Tsz.Modules.Users.Contracts;
using Tsz.Modules.Users.Domain.Leaves;
using Tsz.Modules.Users.Domain.LeaveTypes;
using Tsz.Modules.Users.Features;
using Tsz.Api.Tests.Builders;
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Tests.Modules.Users.Features;

public class UpdateUserLeavesHandlerTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private const int Year = 2026;

    private (Mock<IUnitOfWork> uow, Mock<IRepository<UserLeave>> repo, Mock<IRepository<LeaveType>> ltRepo)
        SetupMocks(List<UserLeave> rows, List<UserLeave> allRowsAfterSave, List<LeaveType> leaveTypes)
    {
        var repo = new Mock<IRepository<UserLeave>>();

        // First call (load by ids): return filtered rows matching the id set
        repo.SetupSequence(r => r.GetAllAsListAsync(
                It.IsAny<Expression<Func<UserLeave, bool>>>(),
                It.IsAny<CancellationToken>(),
                false))
            .ReturnsAsync(rows)
            .ReturnsAsync(allRowsAfterSave);

        var ltRepo = new Mock<IRepository<LeaveType>>();
        ltRepo.Setup(r => r.GetAllAsListAsync(null, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(leaveTypes);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<UserLeave>()).Returns(repo.Object);
        uow.Setup(u => u.RepositoryFor<LeaveType>()).Returns(ltRepo.Object);

        return (uow, repo, ltRepo);
    }

    [Fact]
    public async Task HandleAsync_BulkUpdate_MutatesAllRows_ReturnedFullYearSet()
    {
        var ltVerlof = LeaveTypeBuilder.Limited("Verlof", 20m);
        var ltZiekte = LeaveTypeBuilder.Unlimited("Ziekte");

        var leaveVerlof = UserLeaveBuilder.Build(_userId, ltVerlof.Id, Year, 20m);
        var leaveZiekte = UserLeaveBuilder.Build(_userId, ltZiekte.Id, Year, null);

        var (uow, _, _) = SetupMocks(
            rows: [leaveVerlof, leaveZiekte],
            allRowsAfterSave: [leaveVerlof, leaveZiekte],
            leaveTypes: [ltVerlof, ltZiekte]);

        var handler = new UpdateUserLeavesHandler(uow.Object);
        var command = new UpdateUserLeavesCommand(_userId, Year,
        [
            new UpdateUserLeavesItem(leaveVerlof.Id, 22m),
            new UpdateUserLeavesItem(leaveZiekte.Id, null),
        ]);

        var result = await handler.HandleAsync(command);

        result.Count.ShouldBe(2);
        leaveVerlof.TotalDays.ShouldBe(22m);
        leaveZiekte.TotalDays.ShouldBeNull();
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ResponseIncludesJoinedLeaveTypeName()
    {
        var ltVerlof = LeaveTypeBuilder.Limited("Verlof", 20m);
        var leave = UserLeaveBuilder.Build(_userId, ltVerlof.Id, Year, 20m);

        var (uow, _, _) = SetupMocks(
            rows: [leave],
            allRowsAfterSave: [leave],
            leaveTypes: [ltVerlof]);

        var handler = new UpdateUserLeavesHandler(uow.Object);
        var command = new UpdateUserLeavesCommand(_userId, Year,
            [new UpdateUserLeavesItem(leave.Id, 25m)]);

        var result = await handler.HandleAsync(command);

        result.Count.ShouldBe(1);
        result[0].LeaveTypeName.ShouldBe("Verlof");
        result[0].DefaultAllowed.ShouldBe(LeaveAllowed.Limited);
    }

    [Fact]
    public async Task HandleAsync_SubsetUpdate_UntouchedRowsPreservedInResponse()
    {
        var ltVerlof = LeaveTypeBuilder.Limited("Verlof", 20m);
        var ltAdv = LeaveTypeBuilder.Limited("ADV", 5m);

        var leaveVerlof = UserLeaveBuilder.Build(_userId, ltVerlof.Id, Year, 20m);
        var leaveAdv = UserLeaveBuilder.Build(_userId, ltAdv.Id, Year, 5m);

        // Handler loads subset for the update, then all rows for the response
        var repo = new Mock<IRepository<UserLeave>>();
        repo.SetupSequence(r => r.GetAllAsListAsync(
                It.IsAny<Expression<Func<UserLeave, bool>>>(),
                It.IsAny<CancellationToken>(),
                false))
            .ReturnsAsync([leaveVerlof])           // first call: only the item being updated
            .ReturnsAsync([leaveVerlof, leaveAdv]); // second call: full year set

        var ltRepo = new Mock<IRepository<LeaveType>>();
        ltRepo.Setup(r => r.GetAllAsListAsync(null, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync([ltVerlof, ltAdv]);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<UserLeave>()).Returns(repo.Object);
        uow.Setup(u => u.RepositoryFor<LeaveType>()).Returns(ltRepo.Object);

        var handler = new UpdateUserLeavesHandler(uow.Object);
        var command = new UpdateUserLeavesCommand(_userId, Year,
            [new UpdateUserLeavesItem(leaveVerlof.Id, 22m)]);

        var result = await handler.HandleAsync(command);

        result.Count.ShouldBe(2);
        result.ShouldContain(r => r.LeaveTypeName == "Verlof" && r.TotalDays == 22m);
        result.ShouldContain(r => r.LeaveTypeName == "ADV" && r.TotalDays == 5m);
    }
}
