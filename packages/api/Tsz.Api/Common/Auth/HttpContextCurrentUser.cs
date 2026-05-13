using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Tsz.Api.Common.Persistence;
using Tsz.Api.Modules.Users;

namespace Tsz.Api.Common.Auth;

public sealed class HttpContextCurrentUser(
    IHttpContextAccessor httpContextAccessor,
    AppDbContext db) : ICurrentUser
{
    private const string OidClaim = "oid";
    private const string OidClaimLong = "http://schemas.microsoft.com/identity/claims/objectidentifier";
    private const string EmailClaim = "email";
    private const string PreferredUsernameClaim = "preferred_username";
    private const string NameClaim = "name";

    private User? _cached;
    private bool _loaded;

    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public string? EntraOid =>
        FirstClaim(OidClaim, OidClaimLong, ClaimTypes.NameIdentifier, "sub");

    public string? Email =>
        FirstClaim(EmailClaim, PreferredUsernameClaim, ClaimTypes.Email);

    public string? Name => FirstClaim(NameClaim, ClaimTypes.Name);

    public async Task<User?> GetAsync(CancellationToken ct = default)
    {
        if (_loaded) return _cached;

        var oid = EntraOid;
        var email = Email;

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

    private string? FirstClaim(params string[] types)
    {
        var principal = Principal;
        if (principal is null) return null;

        foreach (var type in types)
        {
            var value = principal.FindFirst(type)?.Value;
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }
        return null;
    }
}
