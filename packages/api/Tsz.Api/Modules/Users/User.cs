using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Modules.Users;

public class User : IEntityBase
{
    public Guid Id { get; set; }
    public string? EntraOid { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    private User() { }

    public static User Create(string firstName, string lastName, string email, UserRole role) => new()
    {
        Id = Guid.NewGuid(),
        FirstName = firstName,
        LastName = lastName,
        Email = email,
        Role = role,
    };

    public void Rename(string firstName, string lastName)
    {
        FirstName = firstName;
        LastName = lastName;
    }

    public void ChangeRole(UserRole role) => Role = role;
    public void LinkEntraOid(string oid) => EntraOid = oid;
    public void SoftDelete(DateTimeOffset at) => DeletedAt = at;
}
