using Tsz.Infrastructure.Abstractions;

namespace Tsz.Infrastructure.Common.Pagination;

public static class RepositoryPaginationExtensions
{
    public static Task<KeysetPage<TDto>> GetPagedAsync<TEntity, TDto>(
        this IRepository<TEntity> repo,
        KeysetQueryOptions options,
        SortMap<TEntity> sortMap,
        SearchableField<TEntity>[] searchableFields,
        Expression<Func<TEntity, TDto>> projection,
        CancellationToken ct = default)
        where TEntity : class, ISoftDeletable
    {
        Expression<Func<TEntity, bool>>? filter = options.DeletedOnly
            ? e => e.DeletedAt != null
            : null;
        return repo.GetPagedAsync(
            options,
            sortMap,
            searchableFields,
            projection,
            filter,
            ct,
            ignoreQueryFilters: options.DeletedOnly);
    }
}
