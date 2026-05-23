using Tsz.Infrastructure.Abstractions;
using Tsz.Modules.LeaveTypes.Contracts;
using Tsz.Modules.LeaveTypes.Domain.LeaveTypes;
using Tsz.Modules.LeaveTypes.Domain.Leaves;

namespace Tsz.Modules.LeaveTypes.Features;

public sealed record GetUserLeavesQuery(Guid UserId, int Year) : IQuery<IReadOnlyList<UserLeaveDto>>;

public sealed class GetUserLeavesHandler(IUnitOfWork uow)
    : IQueryHandler<GetUserLeavesQuery, IReadOnlyList<UserLeaveDto>>
{
    public async Task<IReadOnlyList<UserLeaveDto>> HandleAsync(GetUserLeavesQuery query, CancellationToken ct = default)
    {
        var leaves = await uow.RepositoryFor<UserLeave>()
            .GetAllAsListAsync(ul => ul.UserId == query.UserId && ul.Year == query.Year, ct: ct);

        if (!leaves.Any())
            return [];

        var leaveTypeIds = leaves.Select(ul => ul.LeaveTypeId).ToHashSet();
        var leaveTypes = (await uow.RepositoryFor<LeaveType>()
                .GetAllAsListAsync(ct: ct))
            .Where(lt => leaveTypeIds.Contains(lt.Id))
            .ToDictionary(lt => lt.Id);

        return leaves
            .Select(ul =>
            {
                leaveTypes.TryGetValue(ul.LeaveTypeId, out var lt);
                return UserLeaveDto.ToDto(ul, lt?.Name ?? string.Empty, lt?.DefaultAllowed ?? LeaveAllowed.NotAllowed);
            })
            .ToList();
    }
}
