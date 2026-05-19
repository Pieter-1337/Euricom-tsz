using System.Linq.Expressions;
using Moq;
using Shouldly;
using Tsz.Api.Modules.Customers;
using Tsz.Api.Modules.Customers.Features;
using Tsz.Api.Modules.Users;
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Tests.Modules.Customers.Features;

public class CreateCustomerValidatorTests
{
    private static CreateCustomerValidator BuildValidator(bool userExists = true, bool userIsClientManager = true)
    {
        var userRepo = new Mock<IRepository<User>>();
        userRepo.SetupSequence(r => r.ExistsAsync(
                It.IsAny<Expression<Func<User, bool>>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
            .ReturnsAsync(userExists)
            .ReturnsAsync(userIsClientManager);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<User>()).Returns(userRepo.Object);

        return new CreateCustomerValidator(uow.Object);
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
