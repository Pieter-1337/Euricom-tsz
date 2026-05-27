namespace Tsz.Infrastructure.Auth;

/// Resolves the <em>real</em> caller strictly from JWT claims — impersonation-unaware.
/// Used by <c>ImpersonationMiddleware</c> to validate the initiating Admin before the
/// effective <c>ICurrentUserResolver</c> runs.
/// Interface lives in <c>Tsz.Infrastructure</c> so <c>Tsz.Api</c> middleware can depend on it;
/// implementation lives in <c>Tsz.Modules.Users</c> where the <see cref="ResolvedUser"/> aggregate is.
public interface IRealUserResolver
{
    Task<ResolvedUser?> ResolveRealAsync(CancellationToken ct = default);
}
