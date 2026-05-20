using Moq;
using Shouldly;
using Tsz.Modules.Customers.Domain.Customers;
using Tsz.Modules.Customers.Features;
using Tsz.Api.Tests.Builders;
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Tests.Modules.Customers.Features;

public class UpdateCustomerHandlerTests
{
    [Fact]
    public async Task HandleAsync_Existing_UpdatesFieldsButPreservesNumber()
    {
        var existing = CustomerBuilder.Build(42).WithName("Old Inc").WithContact("old@x.com");
        var repo = new Mock<IRepository<Customer>>();
        repo.Setup(r => r.GetByIdAsync(existing.Id, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(existing);
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<Customer>()).Returns(repo.Object);

        var handler = new UpdateCustomerHandler(uow.Object);

        var result = await handler.HandleAsync(new UpdateCustomerCommand(
            existing.Id,
            "New Inc",
            new ContactPersonDto("Bob", "new@x.com"),
            new AddressDto("Park 7", null, "Antwerp", "BE"),
            ClientManagerId: null));

        result.Number.ShouldBe(42);
        result.Name.ShouldBe("New Inc");
        result.ContactPerson.Email.ShouldBe("new@x.com");
        result.ContactPerson.Name.ShouldBe("Bob");
        result.Address.City.ShouldBe("Antwerp");

        existing.Number.ShouldBe(42);
        existing.Name.ShouldBe("New Inc");
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AssignsAndClearsClientManager()
    {
        var managerId = Guid.NewGuid();
        var existing = CustomerBuilder.Build(5);
        var repo = new Mock<IRepository<Customer>>();
        repo.Setup(r => r.GetByIdAsync(existing.Id, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(existing);
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<Customer>()).Returns(repo.Object);

        var handler = new UpdateCustomerHandler(uow.Object);

        var assigned = await handler.HandleAsync(new UpdateCustomerCommand(
            existing.Id,
            "Acme",
            new ContactPersonDto(null, "x@x.com"),
            null,
            ClientManagerId: managerId));
        assigned.ClientManagerId.ShouldBe(managerId);
        existing.ClientManagerId.ShouldBe(managerId);

        var cleared = await handler.HandleAsync(new UpdateCustomerCommand(
            existing.Id,
            "Acme",
            new ContactPersonDto(null, "x@x.com"),
            null,
            ClientManagerId: null));
        cleared.ClientManagerId.ShouldBeNull();
        existing.ClientManagerId.ShouldBeNull();
    }

    [Fact]
    public async Task HandleAsync_NullAddress_ResetsToEmpty()
    {
        var existing = CustomerBuilder.Build(3).WithAddress("Old St", "1000", "Ghent", "BE");
        var repo = new Mock<IRepository<Customer>>();
        repo.Setup(r => r.GetByIdAsync(existing.Id, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(existing);
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<Customer>()).Returns(repo.Object);

        var handler = new UpdateCustomerHandler(uow.Object);

        var result = await handler.HandleAsync(new UpdateCustomerCommand(
            existing.Id,
            "Same",
            new ContactPersonDto(null, "x@x.com"),
            null,
            ClientManagerId: null));

        result.Address.Street.ShouldBeNull();
        result.Address.City.ShouldBeNull();
    }
}
