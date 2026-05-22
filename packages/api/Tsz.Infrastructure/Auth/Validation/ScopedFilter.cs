using System.Linq.Expressions;
using Tsz.Infrastructure.Common.Pagination;

namespace Tsz.Infrastructure.Auth.Validation;

public static class ScopedFilter
{
    /// Resolves the caller's ownership filter for <paramref name="policy"/> and AND-combines
    /// it with <paramref name="additional"/>. When the caller has full access (e.g. Admin),
    /// returns <paramref name="additional"/> unchanged.
    public static async Task<Expression<Func<T, bool>>> ComposeAsync<T>(
        IDataScopeAccessor scope,
        OwnershipPolicy<T> policy,
        Expression<Func<T, bool>> additional,
        CancellationToken ct = default)
    {
        var ownership = await scope.OwnershipFilterAsync(policy, ct);
        return ownership is null ? additional : ownership.And(additional);
    }
}
