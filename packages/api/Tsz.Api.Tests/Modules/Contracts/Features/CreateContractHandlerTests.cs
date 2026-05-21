using System.Linq.Expressions;
using Moq;
using Shouldly;
using Tsz.Api.Tests.Builders;
using Tsz.Infrastructure.Abstractions;
using Tsz.Modules.Contracts.Domain.Contracts;
using Tsz.Modules.Contracts.Features;
using Tsz.Modules.Customers.Contracts;
using Tsz.Modules.Customers.Contracts.Queries;

namespace Tsz.Api.Tests.Modules.Contracts.Features;

public class CreateContractHandlerTests
{
    private static (Mock<IUnitOfWork> uow, Mock<IRepository<Contract>> repo, Mock<ICustomersAccessModule> customers) BuildMocks(
        IEnumerable<Contract>? existing = null,
        Guid? customerClientManagerId = null)
    {
        var repo = new Mock<IRepository<Contract>>();
        repo.Setup(r => r.GetAllAsListAsync(
                It.IsAny<Expression<Func<Contract, bool>>?>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
            .ReturnsAsync(existing?.ToList() ?? []);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<Contract>()).Returns(repo.Object);

        var customers = new Mock<ICustomersAccessModule>();
        customers
            .Setup(m => m.ExecuteQueryAsync(It.IsAny<GetCustomerClientManagerIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(customerClientManagerId);

        return (uow, repo, customers);
    }

    [Fact]
    public async Task HandleAsync_NoExisting_AssignsNumberOne()
    {
        var (uow, repo, customers) = BuildMocks();
        var handler = new CreateContractHandler(uow.Object, customers.Object);

        var dto = await handler.HandleAsync(new CreateContractCommand(
            Subject: "Engagement Alpha",
            CustomerId: Guid.NewGuid(),
            Start: new DateOnly(2026, 1, 1),
            End: null));

        dto.Number.ShouldBe(1);
        dto.Subject.ShouldBe("Engagement Alpha");
        repo.Verify(r => r.Add(It.Is<Contract>(c => c.Number == 1)), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithExisting_AssignsNextNumber()
    {
        var (uow, repo, customers) = BuildMocks(existing: [ContractBuilder.Build(1), ContractBuilder.Build(5)]);
        var handler = new CreateContractHandler(uow.Object, customers.Object);

        var dto = await handler.HandleAsync(new CreateContractCommand(
            Subject: "Engagement Beta",
            CustomerId: Guid.NewGuid(),
            Start: new DateOnly(2026, 2, 1),
            End: new DateOnly(2026, 12, 31)));

        dto.Number.ShouldBe(6);
        dto.End.ShouldBe(new DateOnly(2026, 12, 31));
        repo.Verify(r => r.Add(It.Is<Contract>(c => c.Number == 6)), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_TrimsSubject()
    {
        var (uow, _, customers) = BuildMocks();
        var handler = new CreateContractHandler(uow.Object, customers.Object);

        var dto = await handler.HandleAsync(new CreateContractCommand(
            Subject: "  Engagement Gamma  ",
            CustomerId: Guid.NewGuid(),
            Start: new DateOnly(2026, 1, 1),
            End: null));

        dto.Subject.ShouldBe("Engagement Gamma");
    }

    [Fact]
    public async Task HandleAsync_CopiesClientManagerFromCustomer()
    {
        var managerId = Guid.NewGuid();
        var (uow, repo, customers) = BuildMocks(customerClientManagerId: managerId);
        var handler = new CreateContractHandler(uow.Object, customers.Object);

        var dto = await handler.HandleAsync(new CreateContractCommand(
            Subject: "Engagement Delta",
            CustomerId: Guid.NewGuid(),
            Start: new DateOnly(2026, 1, 1),
            End: null));

        dto.ClientManagerId.ShouldBe(managerId);
        repo.Verify(r => r.Add(It.Is<Contract>(c => c.ClientManagerId == managerId)), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_CustomerHasNoManager_ContractManagerIsNull()
    {
        var (uow, _, customers) = BuildMocks(customerClientManagerId: null);
        var handler = new CreateContractHandler(uow.Object, customers.Object);

        var dto = await handler.HandleAsync(new CreateContractCommand(
            Subject: "Engagement Echo",
            CustomerId: Guid.NewGuid(),
            Start: new DateOnly(2026, 1, 1),
            End: null));

        dto.ClientManagerId.ShouldBeNull();
    }
}
