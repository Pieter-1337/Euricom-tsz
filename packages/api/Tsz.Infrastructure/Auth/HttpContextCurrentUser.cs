using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Tsz.Infrastructure.Auth;

public sealed class HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private const string OidClaim = "oid";
    private const string OidClaimLong = "http://schemas.microsoft.com/identity/claims/objectidentifier";
    private const string EmailClaim = "email";
    private const string PreferredUsernameClaim = "preferred_username";
    private const string NameClaim = "name";

    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public string? EntraOid =>
        FirstClaim(OidClaim, OidClaimLong, ClaimTypes.NameIdentifier, "sub");

    public string? Email =>
        FirstClaim(EmailClaim, PreferredUsernameClaim, ClaimTypes.Email);

    public string? Name => FirstClaim(NameClaim, ClaimTypes.Name);

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
