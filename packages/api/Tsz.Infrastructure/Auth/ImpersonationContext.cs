namespace Tsz.Infrastructure.Auth;

public sealed class ImpersonationContext : IImpersonationContext
{
    public Guid? TargetUserId { get; private set; }

    public void SetTarget(Guid targetUserId) => TargetUserId = targetUserId;
}
