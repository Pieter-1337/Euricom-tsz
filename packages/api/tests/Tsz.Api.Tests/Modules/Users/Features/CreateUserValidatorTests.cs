using System.Linq.Expressions;
using Moq;
using Shouldly;
using Tsz.Api.Modules.Users;
using Tsz.Api.Modules.Users.Features;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Errors;

namespace Tsz.Api.Tests.Modules.Users.Features;

public class CreateUserValidatorTests
{
    private static (CreateUserValidator validator, Mock<IRepository<User>> userRepo)
        BuildValidator(bool emailExists = false)
    {
        var userRepo = new Mock<IRepository<User>>();
        userRepo
            .Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(emailExists);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<User>()).Returns(userRepo.Object);

        return (new CreateUserValidator(uow.Object), userRepo);
    }

    [Fact]
    public async Task Valid_Passes()
    {
        var (validator, _) = BuildValidator(emailExists: false);
        var result = await validator.ValidateAsync(new CreateUserCommand("Jane", "jane@example.com", UserRole.User));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task EmptyName_Fails()
    {
        var (validator, _) = BuildValidator();
        var result = await validator.ValidateAsync(new CreateUserCommand("", "jane@example.com", UserRole.User));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateUserCommand.Name));
    }

    [Fact]
    public async Task BadEmail_Fails()
    {
        var (validator, _) = BuildValidator();
        var result = await validator.ValidateAsync(new CreateUserCommand("Jane", "not-an-email", UserRole.User));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateUserCommand.Email));
    }

    [Fact]
    public async Task RoleOutOfEnum_Fails()
    {
        var (validator, _) = BuildValidator();
        var result = await validator.ValidateAsync(new CreateUserCommand("Jane", "jane@example.com", (UserRole)999));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateUserCommand.Role));
    }

    [Fact]
    public async Task EmailAlreadyTaken_Fails_WithUserEmailAlreadyExistsError()
    {
        var (validator, _) = BuildValidator(emailExists: true);
        var result = await validator.ValidateAsync(new CreateUserCommand("Jane", "taken@example.com", UserRole.User));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateUserCommand.Email));
        var emailError = result.Errors.First(e => e.PropertyName == nameof(CreateUserCommand.Email)
                                                  && e.ErrorCode == UserErrors.EmailAlreadyExists.Code);
        emailError.ShouldNotBeNull();
        (emailError.CustomState as IErrorCode)?.Category.ShouldBe(ErrorCategory.Conflict);
    }
}
