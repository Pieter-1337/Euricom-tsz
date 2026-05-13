using Tsz.Api.Persistence;

namespace Tsz.Api.Modules.Users;

public class UserSeeder(AppDbContext context)
{
    private const string AdminEmail = "pieter.bracke@euri.com";

    public void Seed()
    {
        if (context.Users.Any(u => u.Email == AdminEmail)) return;

        context.Users.Add(User.Create(
            name: "Pieter Bracke",
            email: AdminEmail,
            role: UserRole.Admin));

        context.SaveChanges();
    }
}
