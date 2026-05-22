using System.Linq.Expressions;
using Tsz.Infrastructure.Auth;

namespace Tsz.Modules.Users.Auth;

public sealed class DataScopeAccessor(ICurrentUserResolver resolver) : IDataScopeAccessor
{
    public async Task<Expression<Func<T, bool>>?> OwnershipFilterAsync<T>(
        OwnershipPolicy<T> policy,
        CancellationToken ct = default)
    {
        var user = await resolver.ResolveAsync(ct);
        if (user is null) return DenyAll<T>();

        if (policy.FullAccessRoles.Any(user.HasRole)) return null;

        return BuildOwnerEquals(policy.OwnerIdSelector, user.Id);
    }

    private static Expression<Func<T, bool>> BuildOwnerEquals<T>(
        Expression<Func<T, Guid?>> ownerIdSelector,
        Guid userId)
    {
        var body = Expression.Equal(
            ownerIdSelector.Body,
            Expression.Constant((Guid?)userId, typeof(Guid?)));
        return Expression.Lambda<Func<T, bool>>(body, ownerIdSelector.Parameters);
    }

    private static Expression<Func<T, bool>> DenyAll<T>()
    {
        var param = Expression.Parameter(typeof(T), "x");
        return Expression.Lambda<Func<T, bool>>(Expression.Constant(false), param);
    }
}
