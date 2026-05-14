using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Modules.LeaveTypes.Features;

public sealed record GetLeaveTypesQuery : IQuery<IReadOnlyList<LeaveTypeDto>>;

public sealed class GetLeaveTypesHandler(IUnitOfWork uow)
    : IQueryHandler<GetLeaveTypesQuery, IReadOnlyList<LeaveTypeDto>>
{
    public async Task<IReadOnlyList<LeaveTypeDto>> HandleAsync(GetLeaveTypesQuery query, CancellationToken ct = default)
    {
        var items = await uow.RepositoryFor<LeaveType>().GetAllAsDtosAsync<LeaveTypeDto>(ct: ct);
        return items.ToList();
    }
}
