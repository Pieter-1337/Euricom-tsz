using Moq;
using Shouldly;
using Tsz.Api.Tests.Builders;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Cqrs;
using Tsz.Modules.Contracts.Domain.Contracts;
using Tsz.Modules.Contracts.Features;

namespace Tsz.Api.Tests.Modules.Contracts.Features;

public class DeleteContractHandlerTests
{
    [Fact]
    public async Task HandleAsync_Existing_SoftDeletesAndReturnsUnit()
    {
        var existing = ContractBuilder.Build(1);
        var repo = new Mock<IRepository<Contract>>();
        repo.Setup(r => r.GetByIdAsync(existing.Id, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(existing);
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<Contract>()).Returns(repo.Object);
        var now = new DateTimeOffset(2026, 5, 21, 12, 0, 0, TimeSpan.Zero);
        var time = new Mock<TimeProvider>();
        time.Setup(t => t.GetUtcNow()).Returns(now);

        var handler = new DeleteContractHandler(uow.Object, time.Object);

        var result = await handler.HandleAsync(new DeleteContractCommand(existing.Id));

        result.ShouldBe(default(Unit));
        existing.DeletedAt.ShouldBe(now);
        repo.Verify(r => r.Remove(It.IsAny<Contract>()), Times.Never);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
