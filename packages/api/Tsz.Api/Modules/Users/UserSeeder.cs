using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Modules.Users;

public class UserSeeder(IUnitOfWork uow)
{
    private const string AdminEmail = "pieter.bracke@euri.com";

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var repo = uow.RepositoryFor<User>();
        if (await repo.ExistsAsync(u => u.Email == AdminEmail, ct)) return;

        repo.Add(User.Create(
            name: "Pieter Bracke",
            email: AdminEmail,
            role: UserRole.Admin));

        await uow.SaveChangesAsync(ct);
    }
}
