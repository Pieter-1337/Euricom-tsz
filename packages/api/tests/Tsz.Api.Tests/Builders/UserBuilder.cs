using FizzWare.NBuilder.Generators;
using Tsz.Api.Modules.Users;

namespace Tsz.Api.Tests.Builders;

public static class UserBuilder
{
    public static User Build()
    {
        var id = Guid.NewGuid();
        var user = User.Create(
            name: "Name_" + id.ToString()[..8],
            email: $"user_{id.ToString()[..8]}@example.com",
            role: UserRole.User);
        user.Id = id;
        return user;
    }

    public static User WithId(this User entity, Guid id)
    {
        entity.Id = id;
        return entity;
    }

    public static User WithName(this User entity, string name)
    {
        entity.Rename(name);
        return entity;
    }

    public static User WithRole(this User entity, UserRole role)
    {
        entity.ChangeRole(role);
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
