using Shouldly;
using Tsz.Api.Modules.Users;

namespace Tsz.Api.Tests.Modules.Users;

public class CreateUserValidatorTests
{
    [Fact]
    public async Task Valid_Passes()
    {
        var validator = new CreateUserValidator();
        var result = await validator.ValidateAsync(new CreateUserCommand("Jane", "jane@example.com", UserRole.User));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task EmptyName_Fails()
    {
        var validator = new CreateUserValidator();
        var result = await validator.ValidateAsync(new CreateUserCommand("", "jane@example.com", UserRole.User));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateUserCommand.Name));
    }

    [Fact]
    public async Task BadEmail_Fails()
    {
        var validator = new CreateUserValidator();
        var result = await validator.ValidateAsync(new CreateUserCommand("Jane", "not-an-email", UserRole.User));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateUserCommand.Email));
    }

    [Fact]
    public async Task RoleOutOfEnum_Fails()
    {
        var validator = new CreateUserValidator();
        var result = await validator.ValidateAsync(new CreateUserCommand("Jane", "jane@example.com", (UserRole)999));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateUserCommand.Role));
    }
}
