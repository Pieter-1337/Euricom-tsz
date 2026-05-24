using Tsz.Infrastructure.Abstractions;
using Tsz.Modules.LeaveTypes.Contracts;
using Tsz.Modules.LeaveTypes.Contracts.Queries;

namespace Tsz.Modules.Timesheets.Features;

public sealed record GetSelectableLeaveTypesQuery(Guid UserId)
    : IQuery<IReadOnlyList<SelectableLeaveTypeDto>>;

public sealed record SelectableLeaveTypeDto(Guid Id, string Name);

public sealed class GetSelectableLeaveTypesHandler(ILeaveTypesAccessModule leaveTypes)
    : IQueryHandler<GetSelectableLeaveTypesQuery, IReadOnlyList<SelectableLeaveTypeDto>>
{
    public async Task<IReadOnlyList<SelectableLeaveTypeDto>> HandleAsync(
        GetSelectableLeaveTypesQuery query,
        CancellationToken ct = default)
    {
        var activeTypes = await leaveTypes.GetActiveLeaveTypesAsync(ct);
        return activeTypes
            .Select(lt => new SelectableLeaveTypeDto(lt.Id, lt.Name))
            .ToList();
    }
}
