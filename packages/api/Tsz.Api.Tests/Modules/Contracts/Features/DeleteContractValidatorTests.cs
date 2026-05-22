using System.Linq.Expressions;
using Moq;
using Shouldly;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Auth;
using Tsz.Modules.Contracts.Domain.Contracts;
using Tsz.Modules.Contracts.Features;

namespace Tsz.Api.Tests.Modules.Contracts.Features;

public class DeleteContractValidatorTests
{
    private static DeleteContractValidator BuildValidator(bool contractExists = true)
    {
        var repo = new Mock<IRepository<Contract>>();
        repo
            .Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<Contract, bool>>>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(contractExists);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<Contract>()).Returns(repo.Object);

        var scope = new Mock<IDataScopeAccessor>();
        scope.Setup(s => s.OwnershipFilterAsync(
                It.IsAny<OwnershipPolicy<Contract>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<Contract, bool>>?)null);

        return new DeleteContractValidator(uow.Object, scope.Object);
    }

    [Fact]
    public async Task Valid_Passes()
    {
        var result = await BuildValidator().ValidateAsync(new DeleteContractCommand(Guid.NewGuid()));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task EmptyId_Fails()
    {
        var result = await BuildValidator().ValidateAsync(new DeleteContractCommand(Guid.Empty));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(DeleteContractCommand.Id));
    }

    [Fact]
    public async Task NotFound_Fails()
    {
        var result = await BuildValidator(contractExists: false).ValidateAsync(new DeleteContractCommand(Guid.NewGuid()));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == ContractErrors.NotFound.Code);
    }
}
