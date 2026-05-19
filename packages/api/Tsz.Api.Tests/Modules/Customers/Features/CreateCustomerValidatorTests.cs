using Shouldly;
using Tsz.Api.Modules.Customers;
using Tsz.Api.Modules.Customers.Features;

namespace Tsz.Api.Tests.Modules.Customers.Features;

public class CreateCustomerValidatorTests
{
    private static CreateCustomerValidator BuildValidator() => new();

    [Fact]
    public async Task Valid_Passes()
    {
        var result = await BuildValidator().ValidateAsync(new CreateCustomerCommand(
            "Acme",
            new ContactPersonDto("Jane", "jane@example.com"),
            new AddressDto("Main 1", "1000", "Brussels", "BE")));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task EmptyName_Fails()
    {
        var result = await BuildValidator().ValidateAsync(new CreateCustomerCommand(
            "",
            new ContactPersonDto(null, "j@x.com"),
            null));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateCustomerCommand.Name));
    }

    [Fact]
    public async Task BadEmail_Fails()
    {
        var result = await BuildValidator().ValidateAsync(new CreateCustomerCommand(
            "Acme",
            new ContactPersonDto(null, "not-an-email"),
            null));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName.Contains("Email"));
    }

    [Fact]
    public async Task EmptyEmail_Fails()
    {
        var result = await BuildValidator().ValidateAsync(new CreateCustomerCommand(
            "Acme",
            new ContactPersonDto(null, ""),
            null));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName.Contains("Email"));
    }

    [Fact]
    public async Task OverLongAddressStreet_Fails()
    {
        var result = await BuildValidator().ValidateAsync(new CreateCustomerCommand(
            "Acme",
            new ContactPersonDto(null, "j@x.com"),
            new AddressDto(new string('x', 257), null, null, null)));
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task InvalidCountryCode_Fails()
    {
        var result = await BuildValidator().ValidateAsync(new CreateCustomerCommand(
            "Acme",
            new ContactPersonDto(null, "j@x.com"),
            new AddressDto(null, null, null, "ZZ")));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName.Contains("Country"));
    }

    [Fact]
    public async Task ValidCountryCode_Passes()
    {
        var result = await BuildValidator().ValidateAsync(new CreateCustomerCommand(
            "Acme",
            new ContactPersonDto(null, "j@x.com"),
            new AddressDto(null, null, null, "BE")));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task EmptyCountryCode_Passes()
    {
        var result = await BuildValidator().ValidateAsync(new CreateCustomerCommand(
            "Acme",
            new ContactPersonDto(null, "j@x.com"),
            new AddressDto("Main 1", null, null, null)));
        result.IsValid.ShouldBeTrue();
    }
}
