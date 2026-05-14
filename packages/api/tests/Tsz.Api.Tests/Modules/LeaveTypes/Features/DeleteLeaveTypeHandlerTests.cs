using Moq;
using Shouldly;
using Tsz.Api.Modules.LeaveTypes;
using Tsz.Api.Modules.LeaveTypes.Features;
using Tsz.Api.Tests.Builders;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Cqrs;

namespace Tsz.Api.Tests.Modules.LeaveTypes.Features;

public class DeleteLeaveTypeHandlerTests
{
    [Fact]
    public async Task HandleAsync_Removes_And_ReturnsUnit()
    {
        var lt = LeaveTypeBuilder.Build();
        var repo = new Mock<IRepository<LeaveType>>();
        repo.Setup(r => r.GetByIdAsync(lt.Id, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(lt);
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<LeaveType>()).Returns(repo.Object);

        var handler = new DeleteLeaveTypeHandler(uow.Object);

        var result = await handler.HandleAsync(new DeleteLeaveTypeCommand(lt.Id));

        result.ShouldBe(default(Unit));
        repo.Verify(r => r.Remove(lt), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
