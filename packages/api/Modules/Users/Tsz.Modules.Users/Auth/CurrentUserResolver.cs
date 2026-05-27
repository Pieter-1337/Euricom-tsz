using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Auth;
using Tsz.Modules.Users.Domain.Users;

namespace Tsz.Modules.Users.Auth;

/// Users-module-internal view of the current caller as the full <see cref="User"/> aggregate.
/// Use this when a handler needs more than identity (e.g. to project a UserDto).
/// For authorization decisions, prefer <see cref="ICurrentUserResolver"/>.
public interface ICurrentUserAccount
{
    Task<User?> GetAsync(CancellationToken ct = default);
}

/// Effective identity wrapper: returns the <em>impersonated</em> user when an active
/// <see cref="IImpersonationContext"/> has a target set by <c>ImpersonationMiddleware</c>;
/// otherwise falls through to the real caller via <see cref="IRealUserResolver"/>.
/// Per-request caching prevents redundant DB hits.
public sealed class CurrentUserResolver(
    RealUserResolver real,
    IImpersonationContext impersonation,
    IUnitOfWork uow)
    : ICurrentUserResolver, ICurrentUserAccount
{
    private User? _cached;
    private bool _loaded;

    async Task<ResolvedUser?> ICurrentUserResolver.ResolveAsync(CancellationToken ct)
    {
        var user = await GetAsync(ct);
        return user is null
            ? null
            : new ResolvedUser(user.Id, user.Roles.Select(r => r.ToString()).ToArray());
    }

    public async Task<User?> GetAsync(CancellationToken ct = default)
    {
        if (_loaded) return _cached;

        if (impersonation.IsImpersonating)
        {
            _cached = await uow.RepositoryFor<User>()
                .FirstOrDefaultAsync(u => u.Id == impersonation.TargetUserId!.Value, ct);
        }
        else
        {
            _cached = await real.GetRealAsync(ct);
        }

        _loaded = true;
        return _cached;
    }
}
