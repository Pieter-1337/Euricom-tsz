using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Modules.Users;

public class UserSeeder(IUnitOfWork uow, TimeProvider timeProvider)
{
    private const string AdminEmail = "pieter.bracke@euri.com";

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var userRepo = uow.RepositoryFor<User>();
        var admin = await userRepo.FirstOrDefaultAsync(u => u.Email == AdminEmail, ct);
        if (admin is null)
        {
            admin = User.Create(name: "Pieter Bracke", email: AdminEmail, role: UserRole.Admin);
            userRepo.Add(admin);
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
