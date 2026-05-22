using System.Linq.Expressions;
using Moq;
using Shouldly;
using Tsz.Infrastructure.Auth;
using Tsz.Infrastructure.Auth.Validation;

namespace Tsz.Api.Tests.Infrastructure.Auth.Validation;

public class ScopedFilterTests
{
    private sealed class TestEntity
    {
        public Guid Id { get; set; }
        public Guid OwnerId { get; set; }
    }

    private static readonly OwnershipPolicy<TestEntity> Policy = new(
        OwnerEquals: userId => e => e.OwnerId == userId,
        FullAccessRoles: ["Admin"]);

    private static IDataScopeAccessor ScopeReturning(Expression<Func<TestEntity, bool>>? filter)
    {
        var mock = new Mock<IDataScopeAccessor>();
        mock.Setup(s => s.OwnershipFilterAsync(It.IsAny<OwnershipPolicy<TestEntity>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(filter);
        return mock.Object;
    }

    [Fact]
    public async Task WhenOwnershipIsNull_ReturnsAdditionalUnchanged()
    {
        Expression<Func<TestEntity, bool>> additional = e => e.Id == Guid.NewGuid();
        var scope = ScopeReturning(null);

        var result = await ScopedFilter.ComposeAsync(scope, Policy, additional);

        var id = Guid.NewGuid();
        var entity = new TestEntity { Id = id, OwnerId = Guid.NewGuid() };
        result.Compile()(entity).ShouldBe(additional.Compile()(entity));
    }

    [Fact]
    public async Task WhenOwnershipIsPresent_ReturnsAndedExpression()
    {
        var ownerId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        Expression<Func<TestEntity, bool>> ownership = e => e.OwnerId == ownerId;
        Expression<Func<TestEntity, bool>> additional = e => e.Id == entityId;
        var scope = ScopeReturning(ownership);

        var result = await ScopedFilter.ComposeAsync(scope, Policy, additional);

        var compiled = result.Compile();
        var matchesBoth = new TestEntity { Id = entityId, OwnerId = ownerId };
        var wrongOwner = new TestEntity { Id = entityId, OwnerId = Guid.NewGuid() };
        var wrongId = new TestEntity { Id = Guid.NewGuid(), OwnerId = ownerId };

        compiled(matchesBoth).ShouldBeTrue();
        compiled(wrongOwner).ShouldBeFalse();
        compiled(wrongId).ShouldBeFalse();
    }

    [Fact]
    public async Task WhenOwnershipDeniesAll_ReturnsFalseForAll()
    {
        Expression<Func<TestEntity, bool>> denyAll = _ => false;
        Expression<Func<TestEntity, bool>> additional = e => e.Id == Guid.NewGuid();
        var scope = ScopeReturning(denyAll);

        var result = await ScopedFilter.ComposeAsync(scope, Policy, additional);

        var compiled = result.Compile();
        compiled(new TestEntity()).ShouldBeFalse();
    }
}
