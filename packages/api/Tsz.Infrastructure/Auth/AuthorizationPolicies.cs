namespace Tsz.Infrastructure.Auth;

public static class AuthorizationPolicies
{
    public const string RequireAdmin = nameof(RequireAdmin);
    public const string RequireAdminOrAnyClientManager = nameof(RequireAdminOrAnyClientManager);
    public const string RequireAdminOrSelf = nameof(RequireAdminOrSelf);

    public const string AdminRoleName = "Admin";
}
