using System.Linq.Expressions;
using Moq;
using Shouldly;
using Tsz.Infrastructure.Abstractions;
using Tsz.Modules.Customers.Domain.Customers;
using Tsz.Modules.Customers.Features;
using Tsz.Modules.Users.Contracts;
using Tsz.Modules.Users.Contracts.Queries;

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

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<Customer>()).Returns(custRepo.Object);

        var users = new Mock<IUsersAccessModule>();
        users
            .Setup(m => m.ExecuteQueryAsync(It.IsAny<UserExistsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(userExists);
        users
            .Setup(m => m.ExecuteQueryAsync(It.IsAny<UserHasRoleQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(userIsClientManager);

        return new UpdateCustomerValidator(uow.Object, users.Object);
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
