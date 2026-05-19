using System.Linq.Expressions;
using Moq;
using Shouldly;
using Tsz.Api.Modules.Customers;
using Tsz.Api.Modules.Customers.Features;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Errors;

namespace Tsz.Api.Tests.Modules.Customers.Features;

public class UpdateCustomerValidatorTests
{
    private static UpdateCustomerValidator BuildValidator(bool exists = true)
    {
        var repo = new Mock<IRepository<Customer>>();
        repo.Setup(r => r.ExistsAsync(
                It.IsAny<Expression<Func<Customer, bool>>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
            .ReturnsAsync(exists);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<Customer>()).Returns(repo.Object);

        return new UpdateCustomerValidator(uow.Object);
    }

    [Fact]
    public async Task Valid_Passes()
    {
        var result = await BuildValidator().ValidateAsync(new UpdateCustomerCommand(
            Guid.NewGuid(),
            "Acme",
            new ContactPersonDto("Jane", "jane@example.com"),
            null));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Missing_ReturnsNotFoundErrorCode()
    {
        var result = await BuildValidator(exists: false).ValidateAsync(new UpdateCustomerCommand(
            Guid.NewGuid(),
            "Acme",
            new ContactPersonDto(null, "j@x.com"),
            null));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == CustomerErrors.NotFound.Code);
    }

    [Fact]
    public async Task EmptyName_Fails()
    {
        var result = await BuildValidator().ValidateAsync(new UpdateCustomerCommand(
            Guid.NewGuid(),
            "",
            new ContactPersonDto(null, "j@x.com"),
            null));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateCustomerCommand.Name));
    }
}
