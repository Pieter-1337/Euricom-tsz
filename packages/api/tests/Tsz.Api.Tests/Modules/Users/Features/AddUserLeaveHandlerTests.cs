using Moq;
using Shouldly;
using Tsz.Api.Modules.LeaveTypes;
using Tsz.Api.Modules.Users;
using Tsz.Api.Modules.Users.Features;
using Tsz.Api.Tests.Builders;
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Tests.Modules.Users.Features;

public class AddUserLeaveHandlerTests
{
    [Fact]
    public async Task HandleAsync_AddsAndReturnsDto()
    {
        var lt = LeaveTypeBuilder.Build("Verlof", LeaveAllowed.Limited, 20m);

        var ltRepo = new Mock<IRepository<LeaveType>>();
        ltRepo.Setup(r => r.GetByIdAsync(lt.Id, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(lt);

        var ulRepo = new Mock<IRepository<UserLeave>>();

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<LeaveType>()).Returns(ltRepo.Object);
        uow.Setup(u => u.RepositoryFor<UserLeave>()).Returns(ulRepo.Object);

        var handler = new AddUserLeaveHandler(uow.Object);
        var userId = Guid.NewGuid();
        var command = new AddUserLeaveCommand(userId, lt.Id, 15m);

        var dto = await handler.HandleAsync(command);

        dto.ShouldNotBeNull();
        dto.LeaveTypeId.ShouldBe(lt.Id);
        dto.TotalDays.ShouldBe(15m);
        dto.LeaveTypeName.ShouldBe("Verlof");
        ulRepo.Verify(r => r.Add(It.Is<UserLeave>(ul => ul.UserId == userId && ul.LeaveTypeId == lt.Id)), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
