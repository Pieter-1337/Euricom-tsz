using System.Linq.Expressions;
using Moq;
using Shouldly;
using Tsz.Infrastructure.Abstractions;
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

        var handler = new GetContractByIdHandler(uow.Object);
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

        var handler = new GetContractByIdHandler(uow.Object);
        var result = await handler.HandleAsync(new GetContractByIdQuery(Guid.NewGuid()));

        result.ShouldBeNull();
    }
}
