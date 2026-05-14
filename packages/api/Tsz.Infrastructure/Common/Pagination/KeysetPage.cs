namespace Tsz.Infrastructure.Common.Pagination;

public sealed record KeysetPage<T>(IReadOnlyList<T> Items, string? NextCursor, int Total);
