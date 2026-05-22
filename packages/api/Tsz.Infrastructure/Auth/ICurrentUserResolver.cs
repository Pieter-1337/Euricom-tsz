namespace Tsz.Infrastructure.Auth;

/// Resolves the current authenticated caller into a thin identity view.
/// Returns <c>null</c> when no caller is authenticated or no matching identity exists.
/// Implementations are scoped per request and cache the lookup.
public interface ICurrentUserResolver
{
    Task<ResolvedUser?> ResolveAsync(CancellationToken ct = default);
}
