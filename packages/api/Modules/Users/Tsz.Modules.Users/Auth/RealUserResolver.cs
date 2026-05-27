using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Auth;
using Tsz.Modules.Users.Domain.Users;

namespace Tsz.Modules.Users.Auth;

/// Resolves the <em>real</em> caller strictly from JWT claims.
/// Keeps the email → EntraOid auto-link on first match.
/// Impersonation-unaware by design.
public sealed class RealUserResolver(ICurrentUser currentUser, IUnitOfWork uow) : IRealUserResolver
{
    private User? _cached;
    private bool _loaded;

    public async Task<ResolvedUser?> ResolveRealAsync(CancellationToken ct = default)
    {
        var user = await GetRealAsync(ct);
        return user is null
            ? null
            : new ResolvedUser(user.Id, user.Roles.Select(r => r.ToString()).ToArray());
    }

    internal async Task<User?> GetRealAsync(CancellationToken ct = default)
    {
        if (_loaded) return _cached;

        var repo = uow.RepositoryFor<User>();
        var oid = currentUser.EntraOid;
        var email = currentUser.Email;

        if (oid is not null)
        {
            _cached = await repo.FirstOrDefaultAsync(u => u.EntraOid == oid, ct);
            if (_cached is not null)
            {
                _loaded = true;
                return _cached;
            }
        }

        if (email is not null)
        {
            var lowered = email.ToLowerInvariant();
            var emailMatch = await repo.FirstOrDefaultAsync(u => u.Email.ToLower() == lowered, ct);

            // Never resolve to an account already bound to a different Entra identity (email reuse/collision); oid is authoritative.
            if (emailMatch is not null && emailMatch.EntraOid is not null && emailMatch.EntraOid != oid) emailMatch = null;

            if (emailMatch is not null && emailMatch.EntraOid is null && oid is not null)
            {
                emailMatch.LinkEntraOid(oid);
                await uow.SaveChangesAsync(ct);
            }

            _cached = emailMatch;
        }

        _loaded = true;
        return _cached;
    }
}
