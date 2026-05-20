using System.Linq.Expressions;
using Moq;
using Shouldly;
using Tsz.Modules.Customers.Domain.Customers;
using Tsz.Modules.Customers.Features;
using Tsz.Api.Tests.Builders;
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Tests.Modules.Customers.Features;

public class CreateCustomerHandlerTests
{
    private static (Mock<IUnitOfWork> uow, Mock<IRepository<Customer>> repo) BuildMocks(IEnumerable<Customer>? existing = null)
    {
        var repo = new Mock<IRepository<Customer>>();
        repo.Setup(r => r.GetAllAsListAsync(
                It.IsAny<Expression<Func<Customer, bool>>?>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
            .ReturnsAsync(existing?.ToList() ?? []);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<Customer>()).Returns(repo.Object);
        return (uow, repo);
    }

    [Fact]
    public async Task HandleAsync_NoExisting_AssignsNumberOne()
    {
        var (uow, repo) = BuildMocks();
        var handler = new CreateCustomerHandler(uow.Object);

        var dto = await handler.HandleAsync(new CreateCustomerCommand(
            "Acme",
            new ContactPersonDto("Jane", "jane@example.com"),
            new AddressDto("Main 1", "1000", "Brussels", "BE"),
            ClientManagerId: null));

        dto.Number.ShouldBe(1);
        dto.Name.ShouldBe("Acme");
        dto.ContactPerson.Email.ShouldBe("jane@example.com");
        dto.ContactPerson.Name.ShouldBe("Jane");
        dto.Address.City.ShouldBe("Brussels");

        repo.Verify(r => r.Add(It.Is<Customer>(c => c.Number == 1 && c.Name == "Acme")), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithExisting_AssignsNextNumber()
    {
        var (uow, repo) = BuildMocks(existing: [CustomerBuilder.Build(1), CustomerBuilder.Build(7)]);
        var handler = new CreateCustomerHandler(uow.Object);

        var dto = await handler.HandleAsync(new CreateCustomerCommand(
            "B Corp",
            new ContactPersonDto(null, "b@x.com"),
            null,
            ClientManagerId: null));

        dto.Number.ShouldBe(8);
        dto.Address.Street.ShouldBeNull();
        repo.Verify(r => r.Add(It.Is<Customer>(c => c.Number == 8)), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_TrimsAndNormalizesAddressFields()
    {
        var (uow, _) = BuildMocks();
        var handler = new CreateCustomerHandler(uow.Object);

        var dto = await handler.HandleAsync(new CreateCustomerCommand(
            "Acme",
            new ContactPersonDto("  Jane  ", "jane@example.com"),
            new AddressDto("  Main 1  ", "   ", "Brussels", ""),
            ClientManagerId: null));

        dto.Address.Street.ShouldBe("Main 1");
        dto.Address.Zip.ShouldBeNull();
        dto.Address.Country.ShouldBeNull();
        dto.ContactPerson.Name.ShouldBe("Jane");
    }
}
