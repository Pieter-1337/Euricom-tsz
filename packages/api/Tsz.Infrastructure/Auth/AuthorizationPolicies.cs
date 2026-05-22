namespace Tsz.Infrastructure.Auth;

public static class AuthorizationPolicies
{
    public const string RequireAdmin = nameof(RequireAdmin);
    public const string RequireClientManager = nameof(RequireClientManager);
}
