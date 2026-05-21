using System.Linq.Expressions;
using Moq;
using Shouldly;
using Tsz.Infrastructure.Abstractions;
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

        return new DeleteContractValidator(uow.Object);
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
