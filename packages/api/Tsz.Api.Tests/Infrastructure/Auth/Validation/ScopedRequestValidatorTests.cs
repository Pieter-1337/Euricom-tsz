using System.Linq.Expressions;
using FluentValidation;
using Moq;
using Shouldly;
using Tsz.Api.Tests.Builders;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Auth;
using Tsz.Infrastructure.Auth.Validation;
using Tsz.Modules.Customers.Domain.Customers;
using Tsz.Modules.Customers.Features;

namespace Tsz.Api.Tests.Infrastructure.Auth.Validation;

public class ScopedRequestValidatorTests
{
    private sealed record TestCommand(Guid EntityId, Guid? ManagerId);

    private sealed class TestValidator : ScopedRequestValidator<TestCommand>
    {
        public TestValidator(IUnitOfWork uow, IDataScopeAccessor scope, ICurrentUserResolver currentUser)
            : base(uow, scope, currentUser)
        {
            RuleForOwnedEntity(x => x.EntityId, GetCustomersPagedHandler.ScopePolicy, id => c => c.Id == id)
                .WithMessage("Entity not found or not accessible.");
            RuleForSelfAssignedManager(x => x.ManagerId)
                .WithMessage("Non-admin must assign themselves.");
        }
    }

    private static (Mock<IUnitOfWork> uow, Mock<IRepository<Customer>> repo) BuildUow(bool exists = true)
    {
        var repo = new Mock<IRepository<Customer>>();
        repo.Setup(r => r.ExistsAsync(
                It.IsAny<Expression<Func<Customer, bool>>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
            .ReturnsAsync(exists);
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<Customer>()).Returns(repo.Object);
        return (uow, repo);
    }

    private static IDataScopeAccessor AdminScope()
    {
        var mock = new Mock<IDataScopeAccessor>();
        mock.Setup(s => s.OwnershipFilterAsync(It.IsAny<OwnershipPolicy<Customer>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<Customer, bool>>?)null);
        return mock.Object;
    }

    private static ICurrentUserResolver ResolverFor(Guid userId, params string[] roles)
    {
        var mock = new Mock<ICurrentUserResolver>();
        mock.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResolvedUser(userId, roles));
        return mock.Object;
    }

    [Fact]
    public async Task RuleForOwnedEntity_EntityExists_Passes()
    {
        var (uow, _) = BuildUow(exists: true);
        var validator = new TestValidator(uow.Object, AdminScope(), ResolverFor(Guid.NewGuid(), "Admin"));

        var result = await validator.ValidateAsync(new TestCommand(Guid.NewGuid(), null));

        result.Errors.ShouldNotContain(e => e.PropertyName == nameof(TestCommand.EntityId));
    }

    [Fact]
    public async Task RuleForOwnedEntity_EntityMissing_Fails()
    {
        var (uow, _) = BuildUow(exists: false);
        var validator = new TestValidator(uow.Object, AdminScope(), ResolverFor(Guid.NewGuid(), "Admin"));

        var result = await validator.ValidateAsync(new TestCommand(Guid.NewGuid(), null));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(TestCommand.EntityId));
    }

    [Fact]
    public async Task RuleForOwnedEntity_ComposesFilterWithOwnership()
    {
        var ownerId = Guid.NewGuid();
        var entityId = Guid.NewGuid();

        Expression<Func<Customer, bool>>? captured = null;
        var repo = new Mock<IRepository<Customer>>();
        repo.Setup(r => r.ExistsAsync(
                It.IsAny<Expression<Func<Customer, bool>>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
            .Callback((Expression<Func<Customer, bool>> f, CancellationToken _, bool _) => captured = f)
            .ReturnsAsync(true);
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<Customer>()).Returns(repo.Object);

        var scopeMock = new Mock<IDataScopeAccessor>();
        Expression<Func<Customer, bool>> ownerFilter = c => c.ClientManagerId == ownerId;
        scopeMock.Setup(s => s.OwnershipFilterAsync(It.IsAny<OwnershipPolicy<Customer>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ownerFilter);

        var validator = new TestValidator(uow.Object, scopeMock.Object, ResolverFor(ownerId, "ClientManager"));
        await validator.ValidateAsync(new TestCommand(entityId, ownerId));

        captured.ShouldNotBeNull();
        var compiled = captured!.Compile();
        var matchesBoth = CustomerBuilder.Build(1).WithId(entityId).WithClientManager(ownerId);
        var wrongOwner = CustomerBuilder.Build(2).WithId(entityId).WithClientManager(Guid.NewGuid());
        var wrongId = CustomerBuilder.Build(3).WithClientManager(ownerId);

        compiled(matchesBoth).ShouldBeTrue();
        compiled(wrongOwner).ShouldBeFalse();
        compiled(wrongId).ShouldBeFalse();
    }

    [Fact]
    public async Task RuleForSelfAssignedManager_Admin_Passes()
    {
        var (uow, _) = BuildUow();
        var adminId = Guid.NewGuid();
        var validator = new TestValidator(uow.Object, AdminScope(), ResolverFor(adminId, "Admin"));

        var result = await validator.ValidateAsync(new TestCommand(Guid.NewGuid(), Guid.NewGuid()));

        result.Errors.ShouldNotContain(e => e.PropertyName == nameof(TestCommand.ManagerId));
    }

    [Fact]
    public async Task RuleForSelfAssignedManager_NonAdmin_AssignsSelf_Passes()
    {
        var (uow, _) = BuildUow();
        var userId = Guid.NewGuid();
        var validator = new TestValidator(uow.Object, AdminScope(), ResolverFor(userId, "ClientManager"));

        var result = await validator.ValidateAsync(new TestCommand(Guid.NewGuid(), userId));

        result.Errors.ShouldNotContain(e => e.PropertyName == nameof(TestCommand.ManagerId));
    }

    [Fact]
    public async Task RuleForSelfAssignedManager_NonAdmin_AssignsOther_Fails()
    {
        var (uow, _) = BuildUow();
        var userId = Guid.NewGuid();
        var validator = new TestValidator(uow.Object, AdminScope(), ResolverFor(userId, "ClientManager"));

        var result = await validator.ValidateAsync(new TestCommand(Guid.NewGuid(), Guid.NewGuid()));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(TestCommand.ManagerId));
    }

    [Fact]
    public async Task RuleForSelfAssignedManager_NoUser_Fails()
    {
        var (uow, _) = BuildUow();
        var resolverMock = new Mock<ICurrentUserResolver>();
        resolverMock.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((ResolvedUser?)null);

        var validator = new TestValidator(uow.Object, AdminScope(), resolverMock.Object);

        var result = await validator.ValidateAsync(new TestCommand(Guid.NewGuid(), Guid.NewGuid()));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(TestCommand.ManagerId));
    }
}
