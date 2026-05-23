using Tsz.Infrastructure.Abstractions;

namespace Tsz.Modules.LeaveTypes.Contracts.Queries;

public sealed record GetUserLeaveAllowanceQuery(Guid UserId, Guid LeaveTypeId, int Year) : IQuery<decimal?>;
