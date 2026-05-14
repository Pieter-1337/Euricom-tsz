using Moq;
using Shouldly;
using Tsz.Api.Modules.Users;
using Tsz.Api.Modules.Users.Features;
using Tsz.Api.Tests.Builders;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Cqrs;

namespace Tsz.Api.Tests.Modules.Users.Features;

public class DeleteUserHandlerTests
{
    [Fact]
    public async Task HandleAsync_Existing_SoftDeletesAndReturnsUnit()
    {
        var existing = UserBuilder.Build();
        var repo = new Mock<IRepository<User>>();
        repo.Setup(r => r.GetByIdAsync(existing.Id, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(existing);
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<User>()).Returns(repo.Object);
        var now = new DateTimeOffset(2026, 5, 13, 12, 0, 0, TimeSpan.Zero);
        var time = new Mock<TimeProvider>();
        time.Setup(t => t.GetUtcNow()).Returns(now);

        var handler = new DeleteUserHandler(uow.Object, time.Object);

        var result = await handler.HandleAsync(new DeleteUserCommand(existing.Id));

        result.ShouldBe(default(Unit));
        existing.DeletedAt.ShouldBe(now);
        repo.Verify(r => r.Remove(It.IsAny<User>()), Times.Never);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
