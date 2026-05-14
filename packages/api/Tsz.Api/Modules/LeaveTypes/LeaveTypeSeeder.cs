using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Modules.LeaveTypes;

public class LeaveTypeSeeder(IUnitOfWork uow)
{
    private static readonly (string Name, string Group, LeaveAllowed DefaultAllowed, decimal? DefaultDays)[] Seeds =
    [
        ("Verlof",       "Verlof",  LeaveAllowed.Limited,   20m),
        ("ADV dagen",    "Verlof",  LeaveAllowed.Limited,    5m),
        ("Anciënniteit", "Verlof",  LeaveAllowed.Limited,    0m),
        ("Ziekte",       "Illness", LeaveAllowed.Unlimited, null),
    ];

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var repo = uow.RepositoryFor<LeaveType>();
        foreach (var (name, group, defaultAllowed, defaultDays) in Seeds)
        {
            if (await repo.ExistsAsync(lt => lt.Name == name, ct)) continue;

            repo.Add(LeaveType.Create(name, defaultAllowed, defaultDays, group: group));
        }
        await uow.SaveChangesAsync(ct);
    }
}
