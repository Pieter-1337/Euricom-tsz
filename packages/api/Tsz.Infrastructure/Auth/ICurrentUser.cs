namespace Tsz.Infrastructure.Auth;

public interface ICurrentUser
{
    string? EntraOid { get; }
    string? Email { get; }
    string? Name { get; }
    bool IsAuthenticated { get; }
}
