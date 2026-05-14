using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Modules.Users;

public class User : IEntityBase
{
    public Guid Id { get; set; }
    public string? EntraOid { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    private User() { }

    public static User Create(string name, string email, UserRole role) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Email = email,
        Role = role,
    };

    public void Rename(string name) => Name = name;
    public void ChangeRole(UserRole role) => Role = role;
    public void LinkEntraOid(string oid) => EntraOid = oid;
    public void SoftDelete(DateTimeOffset at) => DeletedAt = at;
}
