using System.Linq.Expressions;
using Moq;
using Shouldly;
using Tsz.Api.Modules.Customers;
using Tsz.Api.Modules.Customers.Features;
using Tsz.Api.Modules.Users;
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Tests.Modules.Customers.Features;

public class UpdateCustomerValidatorTests
{
    private static UpdateCustomerValidator BuildValidator(
        bool exists = true,
        bool userExists = true,
        bool userIsClientManager = true)
    {
        var custRepo = new Mock<IRepository<Customer>>();
        custRepo.Setup(r => r.ExistsAsync(
                It.IsAny<Expression<Func<Customer, bool>>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
            .ReturnsAsync(exists);

        var userRepo = new Mock<IRepository<User>>();
        userRepo.SetupSequence(r => r.ExistsAsync(
                It.IsAny<Expression<Func<User, bool>>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
            .ReturnsAsync(userExists)
            .ReturnsAsync(userIsClientManager);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<Customer>()).Returns(custRepo.Object);
        uow.Setup(u => u.RepositoryFor<User>()).Returns(userRepo.Object);

        return new UpdateCustomerValidator(uow.Object);
    }

    [Fact]
    public async Task Valid_Passes()
    {
        var result = await BuildValidator().ValidateAsync(new UpdateCustomerCommand(
            Guid.NewGuid(),
            "Acme",
            new ContactPersonDto("Jane", "jane@example.com"),
            null,
            ClientManagerId: null));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Missing_ReturnsNotFoundErrorCode()
    {
        var result = await BuildValidator(exists: false).ValidateAsync(new UpdateCustomerCommand(
            Guid.NewGuid(),
            "Acme",
            new ContactPersonDto(null, "j@x.com"),
            null,
            ClientManagerId: null));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == CustomerErrors.NotFound.Code);
    }

    [Fact]
    public async Task EmptyName_Fails()
    {
        var result = await BuildValidator().ValidateAsync(new UpdateCustomerCommand(
            Guid.NewGuid(),
            "",
            new ContactPersonDto(null, "j@x.com"),
            null,
            ClientManagerId: null));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateCustomerCommand.Name));
    }

    [Fact]
    public async Task ClientManager_DoesNotExist_Fails()
    {
        var result = await BuildValidator(userExists: false, userIsClientManager: false)
            .ValidateAsync(new UpdateCustomerCommand(
                Guid.NewGuid(),
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
            .ValidateAsync(new UpdateCustomerCommand(
                Guid.NewGuid(),
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
            .ValidateAsync(new UpdateCustomerCommand(
                Guid.NewGuid(),
                "Acme",
                new ContactPersonDto(null, "j@x.com"),
                null,
                ClientManagerId: Guid.NewGuid()));
        result.IsValid.ShouldBeTrue();
    }
}
