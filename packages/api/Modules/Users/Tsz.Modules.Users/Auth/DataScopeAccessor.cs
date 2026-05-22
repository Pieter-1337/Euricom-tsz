using System.Linq.Expressions;
using Tsz.Infrastructure.Auth;

namespace Tsz.Modules.Users.Auth;

public sealed class DataScopeAccessor(ICurrentUserResolver resolver) : IDataScopeAccessor
{
    public async Task<Expression<Func<T, bool>>?> OwnershipFilterAsync<T>(
        OwnershipPolicy<T> policy,
        CancellationToken ct = default)
    {
        var user = await resolver.ResolveAsync(ct);
        if (user is null) return _ => false;
        if (policy.FullAccessRoles.Any(user.HasRole)) return null;
        return policy.OwnerEquals(user.Id);
    }
}
