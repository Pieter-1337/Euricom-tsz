using Moq;
using Shouldly;
using Tsz.Api.Modules.LeaveTypes;
using Tsz.Api.Modules.LeaveTypes.Features;
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Tests.Modules.LeaveTypes.Features;

public class CreateLeaveTypeHandlerTests
{
    [Fact]
    public async Task HandleAsync_AddsAndReturnsDto()
    {
        var repo = new Mock<IRepository<LeaveType>>();
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<LeaveType>()).Returns(repo.Object);

        var handler = new CreateLeaveTypeHandler(uow.Object);
        var command = new CreateLeaveTypeCommand("Verlof", LeaveAllowed.Limited, 20m, "P001", null, "V", 1);

        var dto = await handler.HandleAsync(command);

        dto.ShouldNotBeNull();
        dto.Name.ShouldBe("Verlof");
        dto.DefaultAllowed.ShouldBe(LeaveAllowed.Limited);
        dto.DefaultDays.ShouldBe(20m);
        repo.Verify(r => r.Add(It.Is<LeaveType>(lt => lt.Name == "Verlof")), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
