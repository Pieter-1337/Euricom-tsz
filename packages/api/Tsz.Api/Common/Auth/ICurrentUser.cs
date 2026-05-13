using Tsz.Api.Modules.Users;

namespace Tsz.Api.Common.Auth;

public interface ICurrentUser
{
    string? EntraOid { get; }
    string? Email { get; }
    string? Name { get; }
    bool IsAuthenticated { get; }

    Task<User?> GetAsync(CancellationToken ct = default);
}
