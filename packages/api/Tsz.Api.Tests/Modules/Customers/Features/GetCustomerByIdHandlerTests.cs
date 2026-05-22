using System.Linq.Expressions;
using Moq;
using Shouldly;
using Tsz.Api.Tests.Builders;
using Tsz.Modules.Customers.Domain.Customers;
using Tsz.Modules.Customers.Features;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Auth;

namespace Tsz.Api.Tests.Modules.Customers.Features;

public class GetCustomerByIdHandlerTests
{
    private static Mock<IDataScopeAccessor> AdminScope()
    {
        var scope = new Mock<IDataScopeAccessor>();
        scope.Setup(s => s.OwnershipFilterAsync(
                It.IsAny<OwnershipPolicy<Customer>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<Customer, bool>>?)null);
        return scope;
    }

    private static Mock<IDataScopeAccessor> ScopedTo(Guid clientManagerId)
    {
        var scope = new Mock<IDataScopeAccessor>();
        Expression<Func<Customer, bool>> filter = c => c.ClientManagerId == clientManagerId;
        scope.Setup(s => s.OwnershipFilterAsync(
                It.IsAny<OwnershipPolicy<Customer>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(filter);
        return scope;
    }

    [Fact]
    public async Task HandleAsync_Existing_ReturnsDto()
    {
        var id = Guid.NewGuid();
        var dto = new CustomerDto(id, 1, "Acme",
            new AddressDto(null, null, null, null),
            new ContactPersonDto(null, "c@x.com"),
            ClientManagerId: null);
        var repo = new Mock<IRepository<Customer>>();
        repo.Setup(r => r.FirstOrDefaultAsDtoAsync<CustomerDto>(
                It.IsAny<Expression<Func<Customer, bool>>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
            .ReturnsAsync(dto);
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<Customer>()).Returns(repo.Object);

        var handler = new GetCustomerByIdHandler(uow.Object, AdminScope().Object);

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

        var handler = new GetCustomerByIdHandler(uow.Object, AdminScope().Object);

        var result = await handler.HandleAsync(new GetCustomerByIdQuery(Guid.NewGuid()));

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ClientManager_ScopedFilter_IsAndedWithIdLookup()
    {
        var id = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        Expression<Func<Customer, bool>>? captured = null;
        var repo = new Mock<IRepository<Customer>>();
        repo.Setup(r => r.FirstOrDefaultAsDtoAsync<CustomerDto>(
                It.IsAny<Expression<Func<Customer, bool>>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
            .Callback((Expression<Func<Customer, bool>> f, CancellationToken _, bool _) => captured = f)
            .ReturnsAsync((CustomerDto?)null);
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<Customer>()).Returns(repo.Object);

        var handler = new GetCustomerByIdHandler(uow.Object, ScopedTo(managerId).Object);

        await handler.HandleAsync(new GetCustomerByIdQuery(id));

        captured.ShouldNotBeNull();
        var test = captured!.Compile();
        var owned = CustomerBuilder.Build(1).WithId(id).WithClientManager(managerId);
        var foreign = CustomerBuilder.Build(2).WithId(id).WithClientManager(Guid.NewGuid());
        var ownedButWrongId = CustomerBuilder.Build(3).WithClientManager(managerId);

        test(owned).ShouldBeTrue();
        test(foreign).ShouldBeFalse();
        test(ownedButWrongId).ShouldBeFalse();
    }
}
