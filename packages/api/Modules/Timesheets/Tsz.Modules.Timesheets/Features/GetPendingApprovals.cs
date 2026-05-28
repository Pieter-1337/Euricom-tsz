using System.Globalization;
using FluentValidation;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Common.Pagination;
using Tsz.Modules.Timesheets.Domain.Timesheets;
using Tsz.Modules.Users.Contracts;
using Tsz.Modules.Users.Contracts.Queries;

namespace Tsz.Modules.Timesheets.Features;

public sealed record GetPendingApprovalsQuery(
    string? Search,
    string? SortBy,
    SortDirection SortDir,
    int PageSize,
    string? Cursor,
    bool DeletedOnly,
    DateOnly? DateFrom,
    DateOnly? DateTo)
    : KeysetQueryOptions(Search, SortBy, SortDir, PageSize, Cursor, DeletedOnly),
      IQuery<KeysetPage<PendingApprovalDto>>
{
    public GetPendingApprovalsQuery() : this(null, null, SortDirection.Asc, 0, null, false, null, null) { }
}

public sealed record PendingApprovalDto(
    Guid UserId,
    string UserName,
    int IsoYear,
    int IsoWeek,
    decimal TotalHours);

public sealed class GetPendingApprovalsQueryValidator : AbstractValidator<GetPendingApprovalsQuery>
{
    internal static readonly HashSet<string> AllowedSortKeys = ["employee", "week", "totalHours"];

    public GetPendingApprovalsQueryValidator()
    {
        RuleFor(q => q.Search)
            .MaximumLength(200)
            .When(q => q.Search is not null)
            .WithMessage("Search term must not exceed 200 characters.");

        RuleFor(q => q.SortBy)
            .Must(key => key is null || AllowedSortKeys.Contains(key))
            .WithMessage(q => $"'{q.SortBy}' is not a sortable column. Allowed: {string.Join(", ", AllowedSortKeys)}.");

        RuleFor(q => q.PageSize)
            .InclusiveBetween(1, 200)
            .WithMessage("PageSize must be between 1 and 200.");
    }
}

// Pending approvals are bounded by team-size × open submissions, so this handler
// loads every submitted week, enriches with user names via the Users module, then
// applies search / sort / page entirely in memory. The shape mirrors the keyset
// pagination pattern used elsewhere, but unlike GetUsersPaged this is NOT
// DB-level paging — the cross-module username lookup forces enrichment first.
public sealed class GetPendingApprovalsHandler(IUnitOfWork uow, IUsersAccessModule users)
    : IQueryHandler<GetPendingApprovalsQuery, KeysetPage<PendingApprovalDto>>
{
    public async Task<KeysetPage<PendingApprovalDto>> HandleAsync(
        GetPendingApprovalsQuery query, CancellationToken ct = default)
    {
        var weeks = (await uow.RepositoryFor<TimesheetWeek>()
            .GetAllAsListAsync(w => w.Status == TimesheetStatus.Submitted, ct))
            .ToList();

        if (weeks.Count == 0)
            return new KeysetPage<PendingApprovalDto>([], null, 0);

        var userIds = weeks.Select(w => w.UserId).Distinct().ToList();
        var nameById = await users.ExecuteQueryAsync(new GetUserNamesByIdsQuery(userIds), ct);

        IEnumerable<PendingApprovalDto> rows = weeks.Select(w => new PendingApprovalDto(
            UserId: w.UserId,
            UserName: nameById.GetValueOrDefault(w.UserId, string.Empty),
            IsoYear: w.IsoYear,
            IsoWeek: w.IsoWeek,
            TotalHours: w.Entries.Sum(e => e.DurationHours) + w.LeaveEntries.Sum(lb => lb.DurationHours)));

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var needle = query.Search.Trim();
            rows = rows.Where(r => MatchesSearch(r, needle));
        }

        if (query.DateFrom is not null || query.DateTo is not null)
        {
            rows = rows.Where(r => WeekOverlapsRange(r.IsoYear, r.IsoWeek, query.DateFrom, query.DateTo));
        }

        rows = ApplySort(rows, query.SortBy, query.SortDir);

        var materialized = rows.ToList();
        return new KeysetPage<PendingApprovalDto>(materialized, null, materialized.Count);
    }

    private static bool MatchesSearch(PendingApprovalDto row, string needle)
    {
        // Match across the visible columns: employee name, formatted week label
        // (YYYY-Www), and the raw year/week numbers — so typing "21" matches week
        // 21, "2026-W21" matches that exact week, and names still match by substring.
        if (row.UserName.Contains(needle, StringComparison.OrdinalIgnoreCase))
            return true;

        var formattedWeek = $"{row.IsoYear}-W{row.IsoWeek:D2}";
        if (formattedWeek.Contains(needle, StringComparison.OrdinalIgnoreCase))
            return true;

        if (row.IsoYear.ToString().Contains(needle, StringComparison.Ordinal))
            return true;

        if (row.IsoWeek.ToString().Contains(needle, StringComparison.Ordinal))
            return true;

        return false;
    }

    // Inclusive overlap: a week's [Mon, Sun] is kept iff it touches [from?, to?].
    // From defaults to "no lower bound", To defaults to "no upper bound".
    private static bool WeekOverlapsRange(int isoYear, int isoWeek, DateOnly? from, DateOnly? to)
    {
        var monday = DateOnly.FromDateTime(ISOWeek.ToDateTime(isoYear, isoWeek, DayOfWeek.Monday));
        var sunday = monday.AddDays(6);
        if (from is { } f && sunday < f) return false;
        if (to is { } t && monday > t) return false;
        return true;
    }

    private static IEnumerable<PendingApprovalDto> ApplySort(
        IEnumerable<PendingApprovalDto> rows, string? sortBy, SortDirection dir)
    {
        var descending = dir == SortDirection.Desc;

        return sortBy switch
        {
            "employee" => descending
                ? rows.OrderByDescending(r => r.UserName).ThenByDescending(r => r.IsoYear).ThenByDescending(r => r.IsoWeek)
                : rows.OrderBy(r => r.UserName).ThenBy(r => r.IsoYear).ThenBy(r => r.IsoWeek),
            "totalHours" => descending
                ? rows.OrderByDescending(r => r.TotalHours).ThenBy(r => r.IsoYear).ThenBy(r => r.IsoWeek)
                : rows.OrderBy(r => r.TotalHours).ThenBy(r => r.IsoYear).ThenBy(r => r.IsoWeek),
            // Default: oldest-week-first (matches prior behavior).
            _ => descending
                ? rows.OrderByDescending(r => r.IsoYear).ThenByDescending(r => r.IsoWeek)
                : rows.OrderBy(r => r.IsoYear).ThenBy(r => r.IsoWeek),
        };
    }
}
