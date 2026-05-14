using System.Linq.Expressions;
using Moq;
using Shouldly;
using Tsz.Api.Modules.Users;
using Tsz.Api.Modules.Users.Features;
using Tsz.Api.Tests.Builders;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Cqrs;

namespace Tsz.Api.Tests.Modules.Users.Features;

public class DeleteUserLeaveHandlerTests
{
    [Fact]
    public async Task HandleAsync_Removes_AndReturnsUnit()
    {
        var userId = Guid.NewGuid();
        var entity = UserLeaveBuilder.Build(userId: userId);

        var repo = new Mock<IRepository<UserLeave>>();
        repo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<UserLeave, bool>>>(), It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(entity);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<UserLeave>()).Returns(repo.Object);

        var handler = new DeleteUserLeaveHandler(uow.Object);

        var result = await handler.HandleAsync(new DeleteUserLeaveCommand(userId, entity.Id));

        result.ShouldBe(default(Unit));
        repo.Verify(r => r.Remove(entity), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
