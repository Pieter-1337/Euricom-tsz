namespace Tsz.Infrastructure.Auth;

/// Request-scoped context that records the active impersonation target.
/// Set by <c>ImpersonationMiddleware</c> after all trust-boundary checks pass.
/// Consumed by the effective <c>ICurrentUserResolver</c> to swap identity.
public interface IImpersonationContext
{
    Guid? TargetUserId { get; }
    bool IsImpersonating => TargetUserId is not null;

    /// Called only by ImpersonationMiddleware after all trust checks pass.
    void SetTarget(Guid targetUserId);
}
