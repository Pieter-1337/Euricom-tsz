using System.Linq.Expressions;
using Moq;
using Shouldly;
using Tsz.Modules.Users.Contracts;
using Tsz.Modules.Users.Domain.Users;
using Tsz.Modules.Users.Features;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Errors;

namespace Tsz.Api.Tests.Modules.Users.Features;

public class CreateUserValidatorTests
{
    private static CreateUserValidator BuildValidator(bool emailExists = false)
    {
        var userRepo = new Mock<IRepository<User>>();
        userRepo
            .Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(emailExists);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<User>()).Returns(userRepo.Object);

        return new CreateUserValidator(uow.Object);
    }

    [Fact]
    public async Task Valid_Passes()
    {
        var validator = BuildValidator(emailExists: false);
        var result = await validator.ValidateAsync(new CreateUserCommand("Jane", "Doe", "jane@example.com", [UserRole.User]));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task EmptyFirstName_Fails()
    {
        var validator = BuildValidator();
        var result = await validator.ValidateAsync(new CreateUserCommand("", "Doe", "jane@example.com", [UserRole.User]));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateUserCommand.FirstName));
    }

    [Fact]
    public async Task EmptyLastName_Fails()
    {
        var validator = BuildValidator();
        var result = await validator.ValidateAsync(new CreateUserCommand("Jane", "", "jane@example.com", [UserRole.User]));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateUserCommand.LastName));
    }

    [Fact]
    public async Task BadEmail_Fails()
    {
        var validator = BuildValidator();
        var result = await validator.ValidateAsync(new CreateUserCommand("Jane", "Doe", "not-an-email", [UserRole.User]));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateUserCommand.Email));
    }

    [Fact]
    public async Task RoleOutOfEnum_Fails()
    {
        var validator = BuildValidator();
        var result = await validator.ValidateAsync(new CreateUserCommand("Jane", "Doe", "jane@example.com", [(UserRole)999]));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName.StartsWith(nameof(CreateUserCommand.Roles)));
    }

    [Fact]
    public async Task EmptyRoles_Fails()
    {
        var validator = BuildValidator();
        var result = await validator.ValidateAsync(new CreateUserCommand("Jane", "Doe", "jane@example.com", []));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateUserCommand.Roles));
    }

    [Fact]
    public async Task DuplicateRoles_Fails()
    {
        var validator = BuildValidator();
        var result = await validator.ValidateAsync(new CreateUserCommand("Jane", "Doe", "jane@example.com", [UserRole.User, UserRole.User]));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateUserCommand.Roles));
    }

    [Fact]
    public async Task MultipleRoles_Passes()
    {
        var validator = BuildValidator(emailExists: false);
        var result = await validator.ValidateAsync(new CreateUserCommand("Jane", "Doe", "jane@example.com", [UserRole.Admin, UserRole.ClientManager]));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task EmailAlreadyTaken_Fails_WithUserEmailAlreadyExistsError()
    {
        var validator = BuildValidator(emailExists: true);
        var result = await validator.ValidateAsync(new CreateUserCommand("Jane", "Doe", "taken@example.com", [UserRole.User]));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateUserCommand.Email));
        var emailError = result.Errors.First(e => e.PropertyName == nameof(CreateUserCommand.Email)
                                                  && e.ErrorCode == UserErrors.EmailAlreadyExists.Code);
        emailError.ShouldNotBeNull();
        (emailError.CustomState as IErrorCode)?.Category.ShouldBe(ErrorCategory.Conflict);
    }
}
