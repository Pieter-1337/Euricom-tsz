using System.Linq.Expressions;
using Moq;
using Shouldly;
using Tsz.Api.Tests.Builders;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Auth;
using Tsz.Modules.Contracts.Domain.Contracts;
using Tsz.Modules.Contracts.Features;

namespace Tsz.Api.Tests.Modules.Contracts.Features;

public class GetContractByIdHandlerTests
{
    private static (Mock<IUnitOfWork> uow, Mock<IRepository<Contract>> repo) BuildMocks()
    {
        var repo = new Mock<IRepository<Contract>>();
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<Contract>()).Returns(repo.Object);
        return (uow, repo);
    }

    private static IDataScopeAccessor AdminScope()
    {
        var scope = new Mock<IDataScopeAccessor>();
        scope.Setup(s => s.OwnershipFilterAsync(
                It.IsAny<OwnershipPolicy<Contract>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<Contract, bool>>?)null);
        return scope.Object;
    }

    private static IDataScopeAccessor ScopedTo(Guid clientManagerId)
    {
        var scope = new Mock<IDataScopeAccessor>();
        Expression<Func<Contract, bool>> filter = c => c.ClientManagerId == clientManagerId;
        scope.Setup(s => s.OwnershipFilterAsync(
                It.IsAny<OwnershipPolicy<Contract>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(filter);
        return scope.Object;
    }

    [Fact]
    public async Task HandleAsync_Found_ReturnsDto()
    {
        var id = Guid.NewGuid();
        var dto = new ContractDto(id, 1, "Engagement", Guid.NewGuid(), Guid.NewGuid(),
            new DateOnly(2026, 1, 1), null, [], []);

        var (uow, repo) = BuildMocks();
        repo.Setup(r => r.FirstOrDefaultAsDtoAsync<ContractDto>(
                It.IsAny<Expression<Func<Contract, bool>>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
            .ReturnsAsync(dto);

        var handler = new GetContractByIdHandler(uow.Object, AdminScope());
        var result = await handler.HandleAsync(new GetContractByIdQuery(id));

        result.ShouldBe(dto);
    }

    [Fact]
    public async Task HandleAsync_NotFound_ReturnsNull()
    {
        var (uow, repo) = BuildMocks();
        repo.Setup(r => r.FirstOrDefaultAsDtoAsync<ContractDto>(
                It.IsAny<Expression<Func<Contract, bool>>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
            .ReturnsAsync((ContractDto?)null);

        var handler = new GetContractByIdHandler(uow.Object, AdminScope());
        var result = await handler.HandleAsync(new GetContractByIdQuery(Guid.NewGuid()));

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ClientManager_ScopedFilter_IsAndedWithIdLookup()
    {
        var id = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        Expression<Func<Contract, bool>>? captured = null;
        var (uow, repo) = BuildMocks();
        repo.Setup(r => r.FirstOrDefaultAsDtoAsync<ContractDto>(
                It.IsAny<Expression<Func<Contract, bool>>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
            .Callback((Expression<Func<Contract, bool>> f, CancellationToken _, bool _) => captured = f)
            .ReturnsAsync((ContractDto?)null);

        var handler = new GetContractByIdHandler(uow.Object, ScopedTo(managerId));
        await handler.HandleAsync(new GetContractByIdQuery(id));

        captured.ShouldNotBeNull();
        var test = captured!.Compile();
        var owned = ContractBuilder.Build(1).WithId(id).WithClientManager(managerId);
        var foreign = ContractBuilder.Build(2).WithId(id).WithClientManager(Guid.NewGuid());
        var ownedButWrongId = ContractBuilder.Build(3).WithClientManager(managerId);

        test(owned).ShouldBeTrue();
        test(foreign).ShouldBeFalse();
        test(ownedButWrongId).ShouldBeFalse();
    }
}
