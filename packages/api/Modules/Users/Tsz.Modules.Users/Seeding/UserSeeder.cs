using Tsz.Infrastructure.Abstractions;
using Tsz.Modules.LeaveTypes.Contracts;
using Tsz.Modules.Users.Contracts;
using Tsz.Modules.Users.Domain.Users;

namespace Tsz.Modules.Users.Seeding;

public class UserSeeder(IUnitOfWork uow, TimeProvider timeProvider, ILeaveTypesAccessModule leaveTypes)
{
    private const string AdminEmail = "pieter.bracke@euri.com";

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var userRepo = uow.RepositoryFor<User>();
        var admin = await userRepo.FirstOrDefaultAsync(u => u.Email == AdminEmail, ct);
        if (admin is null)
        {
            admin = User.Create(firstName: "Pieter", lastName: "Bracke", email: AdminEmail, roles: [UserRole.Admin]);
            userRepo.Add(admin);
            await leaveTypes.SeedUserLeavesAsync(admin.Id, timeProvider.GetUtcNow().Year, ct);
            await uow.SaveChangesAsync(ct);
        }
        else if (string.IsNullOrEmpty(admin.FirstName) || string.IsNullOrEmpty(admin.LastName))
        {
            admin.Rename("Pieter", "Bracke");
            await uow.SaveChangesAsync(ct);
        }
    }
}
