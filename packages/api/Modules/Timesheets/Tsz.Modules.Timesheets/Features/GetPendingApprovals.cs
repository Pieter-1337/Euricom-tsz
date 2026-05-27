using Tsz.Infrastructure.Abstractions;
using Tsz.Modules.Timesheets.Domain.Timesheets;
using Tsz.Modules.Users.Contracts;
using Tsz.Modules.Users.Contracts.Queries;

namespace Tsz.Modules.Timesheets.Features;

public sealed record GetPendingApprovalsQuery : IQuery<IReadOnlyList<PendingApprovalDto>>;

public sealed record PendingApprovalDto(
    Guid UserId,
    string UserName,
    int IsoYear,
    int IsoWeek,
    decimal TotalHours);

public sealed class GetPendingApprovalsHandler(IUnitOfWork uow, IUsersAccessModule users)
    : IQueryHandler<GetPendingApprovalsQuery, IReadOnlyList<PendingApprovalDto>>
{
    public async Task<IReadOnlyList<PendingApprovalDto>> HandleAsync(
        GetPendingApprovalsQuery query, CancellationToken ct = default)
    {
        var weeks = (await uow.RepositoryFor<TimesheetWeek>()
            .GetAllAsListAsync(w => w.Status == TimesheetStatus.Submitted, ct))
            .ToList();

        if (weeks.Count == 0)
            return [];

        var userIds = weeks.Select(w => w.UserId).Distinct().ToList();
        var nameById = await users.ExecuteQueryAsync(new GetUserNamesByIdsQuery(userIds), ct);

        return weeks
            .OrderBy(w => w.IsoYear)
            .ThenBy(w => w.IsoWeek)
            .Select(w => new PendingApprovalDto(
                UserId: w.UserId,
                UserName: nameById.GetValueOrDefault(w.UserId, string.Empty),
                IsoYear: w.IsoYear,
                IsoWeek: w.IsoWeek,
                TotalHours: w.Entries.Sum(e => e.DurationHours) + w.LeaveEntries.Sum(lb => lb.DurationHours)))
            .ToList();
    }
}
