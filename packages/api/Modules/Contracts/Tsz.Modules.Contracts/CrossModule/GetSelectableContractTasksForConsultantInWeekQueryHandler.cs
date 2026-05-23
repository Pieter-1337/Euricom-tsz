using System.Globalization;
using Tsz.Infrastructure.Abstractions;
using Tsz.Modules.Contracts.Contracts.Queries;
using Tsz.Modules.Contracts.Domain.Contracts;

namespace Tsz.Modules.Contracts.CrossModule;

internal sealed class GetSelectableContractTasksForConsultantInWeekQueryHandler(IUnitOfWork uow)
    : IQueryHandler<GetSelectableContractTasksForConsultantInWeekQuery, IReadOnlyList<SelectableContractTaskDto>>
{
    public async Task<IReadOnlyList<SelectableContractTaskDto>> HandleAsync(
        GetSelectableContractTasksForConsultantInWeekQuery query,
        CancellationToken ct = default)
    {
        var weekStart = ISOWeek.ToDateTime(query.IsoYear, query.IsoWeek, DayOfWeek.Monday);
        var weekEnd = weekStart.AddDays(6);
        var weekStartDate = DateOnly.FromDateTime(weekStart);
        var weekEndDate = DateOnly.FromDateTime(weekEnd);

        var contracts = await uow.RepositoryFor<Contract>()
            .GetAllAsListAsync(
                filter: c => c.Consultants.Any(cc => cc.UserId == query.UserId)
                          && c.Start <= weekEndDate
                          && (c.End == null || c.End >= weekStartDate),
                ct: ct);

        return contracts
            .SelectMany(c => c.Tasks
                .Where(t => t.DeletedAt == null)
                .Select(t => new SelectableContractTaskDto(
                    t.Id,
                    t.Name,
                    c.Id,
                    c.Subject,
                    c.CustomerId)))
            .ToList();
    }
}
