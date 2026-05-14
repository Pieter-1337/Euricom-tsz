using Moq;
using Shouldly;
using Tsz.Api.Modules.LeaveTypes;
using Tsz.Api.Modules.LeaveTypes.Features;
using Tsz.Api.Tests.Builders;
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Tests.Modules.LeaveTypes.Features;

public class UpdateLeaveTypeHandlerTests
{
    [Fact]
    public async Task HandleAsync_UpdatesAndReturnsDto()
    {
        var existing = LeaveTypeBuilder.Build("Old", LeaveAllowed.Limited, 10m);
        var repo = new Mock<IRepository<LeaveType>>();
        repo.Setup(r => r.GetByIdAsync(existing.Id, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(existing);
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<LeaveType>()).Returns(repo.Object);

        var handler = new UpdateLeaveTypeHandler(uow.Object);
        var command = new UpdateLeaveTypeCommand(existing.Id, "New", LeaveAllowed.Unlimited, null, null, null, null, null);

        var dto = await handler.HandleAsync(command);

        dto.ShouldNotBeNull();
        dto.Name.ShouldBe("New");
        dto.DefaultAllowed.ShouldBe(LeaveAllowed.Unlimited);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
