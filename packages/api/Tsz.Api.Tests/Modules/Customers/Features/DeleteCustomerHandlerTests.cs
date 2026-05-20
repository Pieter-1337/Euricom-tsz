using Moq;
using Shouldly;
using Tsz.Modules.Customers.Domain.Customers;
using Tsz.Modules.Customers.Features;
using Tsz.Api.Tests.Builders;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Cqrs;

namespace Tsz.Api.Tests.Modules.Customers.Features;

public class DeleteCustomerHandlerTests
{
    [Fact]
    public async Task HandleAsync_Existing_SoftDeletesAndReturnsUnit()
    {
        var existing = CustomerBuilder.Build(1);
        var repo = new Mock<IRepository<Customer>>();
        repo.Setup(r => r.GetByIdAsync(existing.Id, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(existing);
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<Customer>()).Returns(repo.Object);
        var now = new DateTimeOffset(2026, 5, 13, 12, 0, 0, TimeSpan.Zero);
        var time = new Mock<TimeProvider>();
        time.Setup(t => t.GetUtcNow()).Returns(now);

        var handler = new DeleteCustomerHandler(uow.Object, time.Object);

        var result = await handler.HandleAsync(new DeleteCustomerCommand(existing.Id));

        result.ShouldBe(default(Unit));
        existing.DeletedAt.ShouldBe(now);
        repo.Verify(r => r.Remove(It.IsAny<Customer>()), Times.Never);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
