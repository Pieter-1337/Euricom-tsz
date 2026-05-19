using System.Linq.Expressions;
using Moq;
using Shouldly;
using Tsz.Api.Modules.Customers;
using Tsz.Api.Modules.Customers.Features;
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Tests.Modules.Customers.Features;

public class GetCustomerByIdHandlerTests
{
    [Fact]
    public async Task HandleAsync_Existing_ReturnsDto()
    {
        var id = Guid.NewGuid();
        var dto = new CustomerDto(id, 1, "Acme",
            new AddressDto(null, null, null, null),
            new ContactPersonDto(null, "c@x.com"));
        var repo = new Mock<IRepository<Customer>>();
        repo.Setup(r => r.FirstOrDefaultAsDtoAsync<CustomerDto>(
                It.IsAny<Expression<Func<Customer, bool>>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
            .ReturnsAsync(dto);
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<Customer>()).Returns(repo.Object);

        var handler = new GetCustomerByIdHandler(uow.Object);

        var result = await handler.HandleAsync(new GetCustomerByIdQuery(id));

        result.ShouldBe(dto);
    }

    [Fact]
    public async Task HandleAsync_Missing_ReturnsNull()
    {
        var repo = new Mock<IRepository<Customer>>();
        repo.Setup(r => r.FirstOrDefaultAsDtoAsync<CustomerDto>(
                It.IsAny<Expression<Func<Customer, bool>>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
            .ReturnsAsync((CustomerDto?)null);
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<Customer>()).Returns(repo.Object);

        var handler = new GetCustomerByIdHandler(uow.Object);

        var result = await handler.HandleAsync(new GetCustomerByIdQuery(Guid.NewGuid()));

        result.ShouldBeNull();
    }
}
