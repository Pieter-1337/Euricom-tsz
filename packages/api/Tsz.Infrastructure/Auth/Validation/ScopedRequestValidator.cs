using System.Linq.Expressions;
using FluentValidation;
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Infrastructure.Auth.Validation;

public abstract class ScopedRequestValidator<TRequest>(
    IUnitOfWork uow,
    IDataScopeAccessor scope,
    ICurrentUserResolver currentUser)
    : AbstractValidator<TRequest>
{
    protected readonly IUnitOfWork Uow = uow;
    protected readonly IDataScopeAccessor Scope = scope;
    protected readonly ICurrentUserResolver CurrentUser = currentUser;

    /// Validates that the property's value identifies an existing row of <typeparamref name="TEntity"/>
    /// that is accessible to the caller per <paramref name="policy"/>.
    /// <paramref name="idEqualsFactory"/> constructs the id-equality predicate at the call site.
    protected IRuleBuilderOptions<TRequest, Guid> RuleForOwnedEntity<TEntity>(
        Expression<Func<TRequest, Guid>> property,
        OwnershipPolicy<TEntity> policy,
        Func<Guid, Expression<Func<TEntity, bool>>> idEqualsFactory)
        where TEntity : class, IEntityBase
    {
        return RuleFor(property).MustAsync(async (id, ct) =>
        {
            var filter = await ScopedFilter.ComposeAsync(Scope, policy, idEqualsFactory(id), ct);
            return await Uow.RepositoryFor<TEntity>().ExistsAsync(filter, ct);
        });
    }

    /// Validates that for non-admin callers, the property equals the caller's own user id.
    /// Admins are unrestricted. Returns false when no caller is resolvable.
    protected IRuleBuilderOptions<TRequest, Guid?> RuleForSelfAssignedManager(
        Expression<Func<TRequest, Guid?>> property)
    {
        return RuleFor(property).MustAsync(async (value, ct) =>
        {
            var user = await CurrentUser.ResolveAsync(ct);
            if (user is null) return false;
            if (user.HasRole(AuthorizationPolicies.AdminRoleName)) return true;
            return value == user.Id;
        });
    }
}
