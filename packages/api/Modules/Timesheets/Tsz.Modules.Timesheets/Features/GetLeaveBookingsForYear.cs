using System.Globalization;
using Tsz.Infrastructure.Abstractions;
using Tsz.Modules.LeaveTypes.Contracts;
using Tsz.Modules.Timesheets.Domain.Timesheets;

namespace Tsz.Modules.Timesheets.Features;

public sealed record GetLeaveBookingsForYearQuery(Guid UserId, int Year)
    : IQuery<IReadOnlyList<LeaveBookingForYearDto>>;

public sealed class GetLeaveBookingsForYearHandler(
    IUnitOfWork uow,
    ILeaveTypesAccessModule leaveTypes)
    : IQueryHandler<GetLeaveBookingsForYearQuery, IReadOnlyList<LeaveBookingForYearDto>>
{
    public async Task<IReadOnlyList<LeaveBookingForYearDto>> HandleAsync(
        GetLeaveBookingsForYearQuery query,
        CancellationToken ct = default)
    {
        // Include weeks from adjacent ISO years to capture edge weeks (week 1 / week 52-53).
        var weeks = await uow.RepositoryFor<TimesheetWeek>()
            .GetAllAsListAsync(
                w => w.UserId == query.UserId &&
                     w.IsoYear >= query.Year - 1 &&
                     w.IsoYear <= query.Year + 1,
                ct);

        // Filter by calendar year of the leave booking date.
        var bookings = weeks
            .SelectMany(w => w.LeaveEntries)
            .Where(lb => lb.Date.Year == query.Year)
            .ToList();

        if (bookings.Count == 0)
            return [];

        var distinctLeaveTypeIds = bookings.Select(lb => lb.LeaveTypeId).Distinct().ToList();
        var activeLeaveTypes = await leaveTypes.GetActiveLeaveTypesAsync(ct);
        var leaveTypeById = activeLeaveTypes.ToDictionary(lt => lt.Id);

        return bookings
            .OrderBy(lb => lb.Date)
            .ThenBy(lb => lb.LeaveTypeId)
            .Select(lb =>
            {
                leaveTypeById.TryGetValue(lb.LeaveTypeId, out var lt);
                return new LeaveBookingForYearDto(
                    Date: lb.Date,
                    LeaveTypeId: lb.LeaveTypeId,
                    LeaveTypeName: lt?.Name ?? string.Empty,
                    DurationHours: lb.DurationHours);
            })
            .ToList();
    }
}

public sealed record LeaveBookingForYearDto(
    DateOnly Date,
    Guid LeaveTypeId,
    string LeaveTypeName,
    decimal DurationHours);
