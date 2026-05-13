using Tsz.Api.Modules.Animals;
using Shouldly;

namespace Tsz.Api.Tests.Modules.Animals;

public class CreateAnimalValidatorTests
{
    [Fact]
    public async Task Valid_PassesValidation()
    {
        var validator = new CreateAnimalValidator();
        var result = await validator.ValidateAsync(new CreateAnimalCommand("Rex", "Dog", 2));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task EmptyName_Fails()
    {
        var validator = new CreateAnimalValidator();
        var result = await validator.ValidateAsync(new CreateAnimalCommand("", "Dog", 2));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateAnimalCommand.Name));
    }

    [Fact]
    public async Task AgeOutOfRange_Fails()
    {
        var validator = new CreateAnimalValidator();
        var result = await validator.ValidateAsync(new CreateAnimalCommand("Rex", "Dog", -1));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateAnimalCommand.Age));
    }
}
