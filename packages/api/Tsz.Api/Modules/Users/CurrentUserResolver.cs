using Microsoft.EntityFrameworkCore;
using Tsz.Api.Persistence;
using Tsz.Infrastructure.Auth;

namespace Tsz.Api.Modules.Users;

public interface ICurrentUserResolver
{
    Task<User?> ResolveAsync(CancellationToken ct = default);
}

public sealed class CurrentUserResolver(ICurrentUser currentUser, AppDbContext db) : ICurrentUserResolver
{
    private User? _cached;
    private bool _loaded;

    public async Task<User?> ResolveAsync(CancellationToken ct = default)
    {
        if (_loaded) return _cached;

        var oid = currentUser.EntraOid;
        var email = currentUser.Email;

        if (oid is not null)
        {
            _cached = await db.Users.FirstOrDefaultAsync(u => u.EntraOid == oid, ct);
            if (_cached is not null)
            {
                _loaded = true;
                return _cached;
            }
        }

        if (email is not null)
        {
            var emailMatch = await db.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower(), ct);

            if (emailMatch is not null && emailMatch.EntraOid is null && oid is not null)
            {
                emailMatch.LinkEntraOid(oid);
                await db.SaveChangesAsync(ct);
            }

            _cached = emailMatch;
        }

        _loaded = true;
        return _cached;
    }
}
