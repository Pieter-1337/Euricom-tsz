using System.Linq.Expressions;
using Moq;
using Shouldly;
using Tsz.Api.Modules.Users;
using Tsz.Api.Modules.Users.Features;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Errors;

namespace Tsz.Api.Tests.Modules.Users.Features;

public class UpdateUserValidatorTests
{
    private static (UpdateUserValidator validator, Mock<IRepository<User>> userRepo)
        BuildValidator(bool userExists = true)
    {
        var userRepo = new Mock<IRepository<User>>();
        userRepo
            .Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(userExists);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<User>()).Returns(userRepo.Object);

        return (new UpdateUserValidator(uow.Object), userRepo);
    }

    [Fact]
    public async Task Valid_Passes()
    {
        var (validator, _) = BuildValidator(userExists: true);
        var result = await validator.ValidateAsync(new UpdateUserCommand(Guid.NewGuid(), "Jane", "Doe", UserRole.User));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task EmptyId_Fails()
    {
        var (validator, _) = BuildValidator();
        var result = await validator.ValidateAsync(new UpdateUserCommand(Guid.Empty, "Jane", "Doe", UserRole.User));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateUserCommand.Id));
    }

    [Fact]
    public async Task EmptyFirstName_Fails()
    {
        var (validator, _) = BuildValidator();
        var result = await validator.ValidateAsync(new UpdateUserCommand(Guid.NewGuid(), "", "Doe", UserRole.User));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateUserCommand.FirstName));
    }

    [Fact]
    public async Task EmptyLastName_Fails()
    {
        var (validator, _) = BuildValidator();
        var result = await validator.ValidateAsync(new UpdateUserCommand(Guid.NewGuid(), "Jane", "", UserRole.User));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateUserCommand.LastName));
    }

    [Fact]
    public async Task UserNotFound_Fails_WithNotFoundError()
    {
        var (validator, _) = BuildValidator(userExists: false);
        var result = await validator.ValidateAsync(new UpdateUserCommand(Guid.NewGuid(), "Jane", "Doe", UserRole.User));
        result.IsValid.ShouldBeFalse();
        var idError = result.Errors.First(e => e.PropertyName == nameof(UpdateUserCommand.Id)
                                               && e.ErrorCode == UserErrors.NotFound.Code);
        idError.ShouldNotBeNull();
        (idError.CustomState as IErrorCode)?.Category.ShouldBe(ErrorCategory.NotFound);
    }
}
