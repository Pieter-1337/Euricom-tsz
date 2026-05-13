using Shouldly;
using Tsz.Api.Modules.Users;

namespace Tsz.Api.Tests.Modules.Users;

public class UpdateUserValidatorTests
{
    [Fact]
    public async Task Valid_Passes()
    {
        var validator = new UpdateUserValidator();
        var result = await validator.ValidateAsync(new UpdateUserCommand(Guid.NewGuid(), "Jane", UserRole.User));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task EmptyId_Fails()
    {
        var validator = new UpdateUserValidator();
        var result = await validator.ValidateAsync(new UpdateUserCommand(Guid.Empty, "Jane", UserRole.User));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateUserCommand.Id));
    }

    [Fact]
    public async Task EmptyName_Fails()
    {
        var validator = new UpdateUserValidator();
        var result = await validator.ValidateAsync(new UpdateUserCommand(Guid.NewGuid(), "", UserRole.User));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateUserCommand.Name));
    }
}
