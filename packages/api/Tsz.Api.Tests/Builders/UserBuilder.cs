using FizzWare.NBuilder.Generators;
using Tsz.Modules.Users.Contracts;
using Tsz.Modules.Users.Domain.Users;

namespace Tsz.Api.Tests.Builders;

public static class UserBuilder
{
    public static User Build()
    {
        var id = Guid.NewGuid();
        var user = User.Create(
            firstName: "Test",
            lastName: "User",
            email: $"user_{id.ToString()[..8]}@example.com",
            roles: [UserRole.User]);
        user.Id = id;
        return user;
    }

    public static User WithId(this User entity, Guid id)
    {
        entity.Id = id;
        return entity;
    }

    public static User WithName(this User entity, string firstName, string lastName)
    {
        entity.Rename(firstName, lastName);
        return entity;
    }

    public static User WithFirstName(this User entity, string firstName)
    {
        entity.Rename(firstName, entity.LastName);
        return entity;
    }

    public static User WithLastName(this User entity, string lastName)
    {
        entity.Rename(entity.FirstName, lastName);
        return entity;
    }

    public static User WithRoles(this User entity, params UserRole[] roles)
    {
        entity.SetRoles(roles);
        return entity;
    }

    public static User WithEntraOid(this User entity, string oid)
    {
        entity.LinkEntraOid(oid);
        return entity;
    }

    public static User SoftDeleted(this User entity)
    {
        entity.SoftDelete(DateTimeOffset.UtcNow);
        return entity;
    }
}
