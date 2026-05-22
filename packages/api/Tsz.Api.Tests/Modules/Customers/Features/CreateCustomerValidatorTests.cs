using Moq;
using Shouldly;
using Tsz.Infrastructure.Auth;
using Tsz.Modules.Customers.Domain.Customers;
using Tsz.Modules.Customers.Features;
using Tsz.Modules.Users.Contracts;
using Tsz.Modules.Users.Contracts.Queries;

namespace Tsz.Api.Tests.Modules.Customers.Features;

public class CreateCustomerValidatorTests
{
    private static CreateCustomerValidator BuildValidator(bool userExists = true, bool userIsClientManager = true)
    {
        var users = new Mock<IUsersAccessModule>();
        users
            .Setup(m => m.ExecuteQueryAsync(It.IsAny<UserExistsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(userExists);
        users
            .Setup(m => m.ExecuteQueryAsync(It.IsAny<UserHasRoleQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(userIsClientManager);

        // Default to Admin caller so non-admin self-assignment rule passes for existing tests.
        var resolver = new Mock<ICurrentUserResolver>();
        resolver
            .Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResolvedUser(Guid.NewGuid(), [nameof(UserRole.Admin)]));

        return new CreateCustomerValidator(users.Object, resolver.Object);
    }

    [Fact]
    public async Task Valid_Passes()
    {
        var result = await BuildValidator().ValidateAsync(new CreateCustomerCommand(
            "Acme",
            new ContactPersonDto("Jane", "jane@example.com"),
            new AddressDto("Main 1", "1000", "Brussels", "BE"),
            ClientManagerId: null));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task EmptyName_Fails()
    {
        var result = await BuildValidator().ValidateAsync(new CreateCustomerCommand(
            "",
            new ContactPersonDto(null, "j@x.com"),
            null,
            ClientManagerId: null));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateCustomerCommand.Name));
    }

    [Fact]
    public async Task BadEmail_Fails()
    {
        var result = await BuildValidator().ValidateAsync(new CreateCustomerCommand(
            "Acme",
            new ContactPersonDto(null, "not-an-email"),
            null,
            ClientManagerId: null));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName.Contains("Email"));
    }

    [Fact]
    public async Task EmptyEmail_Fails()
    {
        var result = await BuildValidator().ValidateAsync(new CreateCustomerCommand(
            "Acme",
            new ContactPersonDto(null, ""),
            null,
            ClientManagerId: null));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName.Contains("Email"));
    }

    [Fact]
    public async Task OverLongAddressStreet_Fails()
    {
        var result = await BuildValidator().ValidateAsync(new CreateCustomerCommand(
            "Acme",
            new ContactPersonDto(null, "j@x.com"),
            new AddressDto(new string('x', 257), null, null, null),
            ClientManagerId: null));
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task InvalidCountryCode_Fails()
    {
        var result = await BuildValidator().ValidateAsync(new CreateCustomerCommand(
            "Acme",
            new ContactPersonDto(null, "j@x.com"),
            new AddressDto(null, null, null, "ZZ"),
            ClientManagerId: null));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName.Contains("Country"));
    }

    [Fact]
    public async Task ValidCountryCode_Passes()
    {
        var result = await BuildValidator().ValidateAsync(new CreateCustomerCommand(
            "Acme",
            new ContactPersonDto(null, "j@x.com"),
            new AddressDto(null, null, null, "BE"),
            ClientManagerId: null));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task EmptyCountryCode_Passes()
    {
        var result = await BuildValidator().ValidateAsync(new CreateCustomerCommand(
            "Acme",
            new ContactPersonDto(null, "j@x.com"),
            new AddressDto("Main 1", null, null, null),
            ClientManagerId: null));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task ClientManager_DoesNotExist_Fails()
    {
        var result = await BuildValidator(userExists: false, userIsClientManager: false)
            .ValidateAsync(new CreateCustomerCommand(
                "Acme",
                new ContactPersonDto(null, "j@x.com"),
                null,
                ClientManagerId: Guid.NewGuid()));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == CustomerErrors.ClientManagerNotFound.Code);
    }

    [Fact]
    public async Task ClientManager_MissingRole_Fails()
    {
        var result = await BuildValidator(userExists: true, userIsClientManager: false)
            .ValidateAsync(new CreateCustomerCommand(
                "Acme",
                new ContactPersonDto(null, "j@x.com"),
                null,
                ClientManagerId: Guid.NewGuid()));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == CustomerErrors.ClientManagerMissingRole.Code);
    }

    [Fact]
    public async Task ClientManager_Valid_Passes()
    {
        var result = await BuildValidator(userExists: true, userIsClientManager: true)
            .ValidateAsync(new CreateCustomerCommand(
                "Acme",
                new ContactPersonDto(null, "j@x.com"),
                null,
                ClientManagerId: Guid.NewGuid()));
        result.IsValid.ShouldBeTrue();
    }
}
