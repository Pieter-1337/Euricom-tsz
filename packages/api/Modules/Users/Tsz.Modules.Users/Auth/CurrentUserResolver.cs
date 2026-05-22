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

public sealed class CurrentUserResolver(ICurrentUser currentUser, IUnitOfWork uow)
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
            var lowered = email.ToLower();
            var emailMatch = await repo.FirstOrDefaultAsync(u => u.Email.ToLower() == lowered, ct);

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
