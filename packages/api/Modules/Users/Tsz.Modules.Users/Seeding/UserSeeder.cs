using Tsz.Infrastructure.Abstractions;
using Tsz.Modules.Users.Contracts;
using Tsz.Modules.Users.Domain.Leaves;
using Tsz.Modules.Users.Domain.LeaveTypes;
using Tsz.Modules.Users.Domain.Users;

namespace Tsz.Modules.Users.Seeding;

public class UserSeeder(IUnitOfWork uow, TimeProvider timeProvider)
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
            await uow.SaveChangesAsync(ct);
        }
        else if (string.IsNullOrEmpty(admin.FirstName) || string.IsNullOrEmpty(admin.LastName))
        {
            admin.Rename("Pieter", "Bracke");
            await uow.SaveChangesAsync(ct);
        }

        var year = timeProvider.GetUtcNow().Year;
        var leaveRepo = uow.RepositoryFor<UserLeave>();
        var existing = (await leaveRepo.GetAllAsListAsync(
            ul => ul.UserId == admin.Id && ul.Year == year, ct))
            .Select(ul => ul.LeaveTypeId)
            .ToHashSet();

        var leaveTypes = await uow.RepositoryFor<LeaveType>().GetAllAsListAsync(ct: ct);
        var added = false;
        foreach (var lt in leaveTypes)
        {
            if (existing.Contains(lt.Id)) continue;
            leaveRepo.Add(UserLeave.Create(admin.Id, lt.Id, year, lt.DefaultDays));
            added = true;
        }
        if (added) await uow.SaveChangesAsync(ct);
    }
}
