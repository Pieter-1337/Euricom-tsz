using Tsz.Infrastructure.Abstractions;
using Tsz.Modules.LeaveTypes.Contracts;

namespace Tsz.Modules.LeaveTypes.Contracts.Queries;

public sealed record ActiveLeaveTypeDto(Guid Id, string Name, LeaveAllowed DefaultAllowed, decimal? DefaultDays);

public sealed record GetActiveLeaveTypesQuery : IQuery<IReadOnlyList<ActiveLeaveTypeDto>>;
