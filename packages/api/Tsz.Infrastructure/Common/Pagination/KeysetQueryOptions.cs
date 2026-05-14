namespace Tsz.Infrastructure.Common.Pagination;

public enum SortDirection { Asc, Desc }

public record KeysetQueryOptions(
    string? Search,
    string? SortBy,
    SortDirection SortDir,
    int PageSize,
    string? Cursor,
    bool DeletedOnly)
{
    public int PageSize { get; init; } = PageSize <= 0 ? 50 : PageSize;
}
