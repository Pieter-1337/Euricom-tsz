using System.Linq.Expressions;
using Moq;
using Shouldly;
using Tsz.Infrastructure.Abstractions;
using Tsz.Modules.Contracts.Contracts.Queries;
using Tsz.Modules.Contracts.CrossModule;
using Tsz.Modules.Contracts.Domain.Contracts;

namespace Tsz.Api.Tests.Modules.Contracts.CrossModule;

public class IsUserReferencedAsClientManagerOnContractQueryHandlerTests
{
    private static (IsUserReferencedAsClientManagerOnContractQueryHandler handler, Mock<IRepository<Contract>> repo) BuildHandler(bool exists)
    {
        var repo = new Mock<IRepository<Contract>>();
        repo
            .Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<Contract, bool>>>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(exists);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<Contract>()).Returns(repo.Object);
        return (new IsUserReferencedAsClientManagerOnContractQueryHandler(uow.Object), repo);
    }

    [Fact]
    public async Task ReturnsTrue_WhenContractExistsForUser()
    {
        var (handler, _) = BuildHandler(exists: true);
        var result = await handler.HandleAsync(new IsUserReferencedAsClientManagerOnContractQuery(Guid.NewGuid()), CancellationToken.None);
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task ReturnsFalse_WhenNoContractExistsForUser()
    {
        var (handler, _) = BuildHandler(exists: false);
        var result = await handler.HandleAsync(new IsUserReferencedAsClientManagerOnContractQuery(Guid.NewGuid()), CancellationToken.None);
        result.ShouldBeFalse();
    }
}
