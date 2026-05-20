using System.Linq.Expressions;
using Moq;
using Shouldly;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Errors;
using Tsz.Modules.Customers.Contracts;
using Tsz.Modules.Customers.Contracts.Queries;
using Tsz.Modules.Users.Contracts;
using Tsz.Modules.Users.Domain.Users;
using Tsz.Modules.Users.Features;

namespace Tsz.Api.Tests.Modules.Users.Features;

public class DeleteUserValidatorTests
{
    private static DeleteUserValidator BuildValidator(
        bool userExists = true,
        bool customerLinked = false)
    {
        var userRepo = new Mock<IRepository<User>>();
        userRepo
            .Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(userExists);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<User>()).Returns(userRepo.Object);

        var customers = new Mock<ICustomersAccessModule>();
        customers
            .Setup(m => m.ExecuteQueryAsync(It.IsAny<IsUserReferencedAsClientManagerQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(customerLinked);

        return new DeleteUserValidator(uow.Object, customers.Object);
    }

    [Fact]
    public async Task Valid_Passes()
    {
        var validator = BuildValidator(userExists: true);
        var result = await validator.ValidateAsync(new DeleteUserCommand(Guid.NewGuid()));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task EmptyId_Fails()
    {
        var validator = BuildValidator();
        var result = await validator.ValidateAsync(new DeleteUserCommand(Guid.Empty));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(DeleteUserCommand.Id));
    }

    [Fact]
    public async Task UserNotFound_Fails_WithNotFoundError()
    {
        var validator = BuildValidator(userExists: false);
        var result = await validator.ValidateAsync(new DeleteUserCommand(Guid.NewGuid()));
        result.IsValid.ShouldBeFalse();
        var error = result.Errors.First(e => e.ErrorCode == UserErrors.NotFound.Code);
        error.ShouldNotBeNull();
        (error.CustomState as IErrorCode)?.Category.ShouldBe(ErrorCategory.NotFound);
    }

    [Fact]
    public async Task ClientManagerStillLinked_Fails()
    {
        var validator = BuildValidator(userExists: true, customerLinked: true);
        var result = await validator.ValidateAsync(new DeleteUserCommand(Guid.NewGuid()));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e =>
            e.ErrorCode == UserErrors.CannotRemoveClientManagerRoleWhileAssigned.Code);
    }

    [Fact]
    public async Task ClientManagerNotLinked_Passes()
    {
        var validator = BuildValidator(userExists: true, customerLinked: false);
        var result = await validator.ValidateAsync(new DeleteUserCommand(Guid.NewGuid()));
        result.IsValid.ShouldBeTrue();
    }
}
