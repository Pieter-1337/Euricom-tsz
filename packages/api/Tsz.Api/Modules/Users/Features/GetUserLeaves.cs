using Tsz.Api.Modules.LeaveTypes;
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Modules.Users.Features;

public sealed record GetUserLeavesQuery(Guid UserId) : IQuery<IReadOnlyList<UserLeaveDto>>;

public sealed class GetUserLeavesHandler(IUnitOfWork uow)
    : IQueryHandler<GetUserLeavesQuery, IReadOnlyList<UserLeaveDto>>
{
    public async Task<IReadOnlyList<UserLeaveDto>> HandleAsync(GetUserLeavesQuery query, CancellationToken ct = default)
    {
        var leaveTypes = await uow.RepositoryFor<LeaveType>().GetAllAsListAsync(ct: ct);
        var leaves = await uow.RepositoryFor<UserLeave>()
            .GetAllAsListAsync(ul => ul.UserId == query.UserId, ct: ct);

        var leaveTypesById = leaveTypes.ToDictionary(lt => lt.Id);
        var leavesByTypeId = leaves.ToDictionary(ul => ul.LeaveTypeId);

        var leaveRepo = uow.RepositoryFor<UserLeave>();
        var needsSave = false;
        foreach (var lt in leaveTypes)
        {
            if (leavesByTypeId.ContainsKey(lt.Id)) continue;
            // Safety net: materialize a missing row so the user always has one per LeaveType.
            var created = UserLeave.Create(query.UserId, lt.Id, lt.DefaultDays);
            leaveRepo.Add(created);
            leavesByTypeId[lt.Id] = created;
            needsSave = true;
        }
        if (needsSave) await uow.SaveChangesAsync(ct);

        return leaves
            .Concat(leavesByTypeId.Values.Where(v => !leaves.Contains(v)))
            .Select(ul =>
            {
                var lt = leaveTypesById.GetValueOrDefault(ul.LeaveTypeId);
                return UserLeaveDto.ToDto(ul, lt?.Name ?? string.Empty, lt?.DefaultAllowed ?? LeaveAllowed.NotAllowed);
            })
            .ToList();
    }
}
