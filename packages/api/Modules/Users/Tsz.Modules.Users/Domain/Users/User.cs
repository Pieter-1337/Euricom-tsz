using Tsz.Infrastructure.Abstractions;
using Tsz.Modules.Users.Contracts;

namespace Tsz.Modules.Users.Domain.Users;

public class User : IEntityBase
{
    public Guid Id { get; set; }
    public string? EntraOid { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public DateTimeOffset? DeletedAt { get; private set; }

    // EF navigation — internal so EF can populate it; external code uses Roles.
    internal List<UserRoleAssignment> RoleAssignments { get; private set; } = [];

    public IReadOnlyCollection<UserRole> Roles =>
        RoleAssignments.Select(r => r.Role).ToList();

    private User() { }

    public static User Create(string firstName, string lastName, string email, IEnumerable<UserRole> roles)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            FirstName = firstName,
            LastName = lastName,
            Email = email,
        };
        user.SetRoles(roles);
        return user;
    }

    public void SetRoles(IEnumerable<UserRole> roles)
    {
        var distinct = roles.Distinct().ToList();
        if (distinct.Count == 0)
            throw new InvalidOperationException("A user must have at least one role.");
        RoleAssignments.Clear();
        foreach (var role in distinct)
            RoleAssignments.Add(new UserRoleAssignment { Role = role });
    }

    public void AddRole(UserRole role)
    {
        if (RoleAssignments.Any(r => r.Role == role)) return;
        RoleAssignments.Add(new UserRoleAssignment { Role = role });
    }

    public void RemoveRole(UserRole role)
    {
        var assignment = RoleAssignments.FirstOrDefault(r => r.Role == role);
        if (assignment is not null)
            RoleAssignments.Remove(assignment);
    }

    public void Rename(string firstName, string lastName)
    {
        FirstName = firstName;
        LastName = lastName;
    }

    public void LinkEntraOid(string oid) => EntraOid = oid;
    public void SoftDelete(DateTimeOffset at) => DeletedAt = at;
}
