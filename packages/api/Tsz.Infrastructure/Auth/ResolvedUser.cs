namespace Tsz.Infrastructure.Auth;

/// Thin identity view of the current caller — no domain entity dependency.
/// Role names use the underlying enum's <c>nameof</c> form (e.g. <c>"Admin"</c>).
public sealed record ResolvedUser(Guid Id, IReadOnlyCollection<string> RoleNames)
{
    public bool HasRole(string roleName) => RoleNames.Contains(roleName);
}
