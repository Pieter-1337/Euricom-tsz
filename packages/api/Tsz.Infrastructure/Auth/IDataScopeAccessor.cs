using System.Linq.Expressions;

namespace Tsz.Infrastructure.Auth;

/// Builds an EF-translatable filter expression that limits a query to rows
/// the current caller is allowed to see, per an <see cref="OwnershipPolicy{T}"/>.
///
/// Return value:
///   <c>null</c>  → caller has full access; no extra filter required.
///   otherwise    → an expression to AND into the query. A "deny everyone"
///                  predicate (<c>x =&gt; false</c>) is returned when no caller is
///                  resolvable, so callers cannot accidentally fall through to
///                  unscoped reads.
public interface IDataScopeAccessor
{
    Task<Expression<Func<T, bool>>?> OwnershipFilterAsync<T>(
        OwnershipPolicy<T> policy,
        CancellationToken ct = default);
}
