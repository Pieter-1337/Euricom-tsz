using System.Linq.Expressions;
using Moq;
using Shouldly;
using Tsz.Api.Modules.Customers;
using Tsz.Api.Modules.Users;
using Tsz.Api.Modules.Users.Features;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Errors;

namespace Tsz.Api.Tests.Modules.Users.Features;

public class UpdateUserValidatorTests
{
    private static UpdateUserValidator BuildValidator(
        bool userExists = true,
        bool customerLinked = false)
    {
        var userRepo = new Mock<IRepository<User>>();
        userRepo
            .Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(userExists);

        var custRepo = new Mock<IRepository<Customer>>();
        custRepo
            .Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<Customer, bool>>>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(customerLinked);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<User>()).Returns(userRepo.Object);
        uow.Setup(u => u.RepositoryFor<Customer>()).Returns(custRepo.Object);

        return new UpdateUserValidator(uow.Object);
    }

    [Fact]
    public async Task Valid_Passes()
    {
        var validator = BuildValidator(userExists: true);
        var result = await validator.ValidateAsync(new UpdateUserCommand(Guid.NewGuid(), "Jane", "Doe", [UserRole.User]));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task EmptyId_Fails()
    {
        var validator = BuildValidator();
        var result = await validator.ValidateAsync(new UpdateUserCommand(Guid.Empty, "Jane", "Doe", [UserRole.User]));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateUserCommand.Id));
    }

    [Fact]
    public async Task EmptyFirstName_Fails()
    {
        var validator = BuildValidator();
        var result = await validator.ValidateAsync(new UpdateUserCommand(Guid.NewGuid(), "", "Doe", [UserRole.User]));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateUserCommand.FirstName));
    }

    [Fact]
    public async Task EmptyLastName_Fails()
    {
        var validator = BuildValidator();
        var result = await validator.ValidateAsync(new UpdateUserCommand(Guid.NewGuid(), "Jane", "", [UserRole.User]));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateUserCommand.LastName));
    }

    [Fact]
    public async Task EmptyRoles_Fails()
    {
        var validator = BuildValidator();
        var result = await validator.ValidateAsync(new UpdateUserCommand(Guid.NewGuid(), "Jane", "Doe", []));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateUserCommand.Roles));
    }

    [Fact]
    public async Task DuplicateRoles_Fails()
    {
        var validator = BuildValidator();
        var result = await validator.ValidateAsync(new UpdateUserCommand(Guid.NewGuid(), "Jane", "Doe", [UserRole.Admin, UserRole.Admin]));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateUserCommand.Roles));
    }

    [Fact]
    public async Task UserNotFound_Fails_WithNotFoundError()
    {
        var validator = BuildValidator(userExists: false);
        var result = await validator.ValidateAsync(new UpdateUserCommand(Guid.NewGuid(), "Jane", "Doe", [UserRole.User]));
        result.IsValid.ShouldBeFalse();
        var idError = result.Errors.First(e => e.PropertyName == nameof(UpdateUserCommand.Id)
                                               && e.ErrorCode == UserErrors.NotFound.Code);
        idError.ShouldNotBeNull();
        (idError.CustomState as IErrorCode)?.Category.ShouldBe(ErrorCategory.NotFound);
    }

    [Fact]
    public async Task RemovingClientManagerRole_WhileCustomerLinked_Fails()
    {
        // userExists=true → mock says user currently has ClientManager; customerLinked=true → blocking
        var validator = BuildValidator(userExists: true, customerLinked: true);
        var result = await validator.ValidateAsync(new UpdateUserCommand(
            Guid.NewGuid(), "Jane", "Doe", [UserRole.User]));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e =>
            e.ErrorCode == UserErrors.CannotRemoveClientManagerRoleWhileAssigned.Code);
    }

    [Fact]
    public async Task RemovingClientManagerRole_WhenNoCustomerLinked_Passes()
    {
        var validator = BuildValidator(userExists: true, customerLinked: false);
        var result = await validator.ValidateAsync(new UpdateUserCommand(
            Guid.NewGuid(), "Jane", "Doe", [UserRole.User]));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task KeepingClientManagerRole_WhileCustomerLinked_Passes()
    {
        var validator = BuildValidator(userExists: true, customerLinked: true);
        var result = await validator.ValidateAsync(new UpdateUserCommand(
            Guid.NewGuid(), "Jane", "Doe", [UserRole.ClientManager]));
        result.IsValid.ShouldBeTrue();
    }
}
