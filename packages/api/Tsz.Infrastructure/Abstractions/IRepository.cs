namespace Tsz.Infrastructure.Abstractions;

public interface IRepository<TEntity> where TEntity : class, IEntityBase
{
    IQueryable<TEntity> GetAll(
        Expression<Func<TEntity, bool>>? filter = null,
        bool ignoreQueryFilters = false);

    Task<IEnumerable<TEntity>> GetAllAsListAsync(
        Expression<Func<TEntity, bool>>? filter = null,
        CancellationToken ct = default,
        bool ignoreQueryFilters = false);

    Task<IEnumerable<TDto>> GetAllAsDtosAsync<TDto>(
        Expression<Func<TEntity, bool>>? filter = null,
        CancellationToken ct = default,
        bool ignoreQueryFilters = false)
        where TDto : class, IEntityDto<TEntity, TDto>;

    Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> filter,
        CancellationToken ct = default,
        bool ignoreQueryFilters = false);

    Task<TDto?> FirstOrDefaultAsDtoAsync<TDto>(
        Expression<Func<TEntity, bool>> filter,
        CancellationToken ct = default,
        bool ignoreQueryFilters = false)
        where TDto : class, IEntityDto<TEntity, TDto>;

    Task<TDto?> FirstOrDefaultAsDtoAsync<TDto, TNavigation>(
        Expression<Func<TEntity, bool>> filter,
        Expression<Func<TEntity, TNavigation>> navigation,
        CancellationToken ct = default,
        bool ignoreQueryFilters = false)
        where TNavigation : class
        where TDto : class, IEntityDto<TNavigation, TDto>;

    Task<TResult?> FirstOrDefaultWithProjectionAsync<TResult>(
        Expression<Func<TEntity, bool>> filter,
        Expression<Func<TEntity, TResult>> projection,
        CancellationToken ct = default,
        bool ignoreQueryFilters = false);

    Task<TEntity?> GetByIdAsync(
        Guid id,
        CancellationToken ct = default,
        bool ignoreQueryFilters = false);

    Task<bool> ExistsAsync(
        Guid id,
        CancellationToken ct = default,
        bool ignoreQueryFilters = false);

    Task<bool> ExistsAsync(
        Expression<Func<TEntity, bool>> filter,
        CancellationToken ct = default,
        bool ignoreQueryFilters = false);

    void Add(TEntity entity);

    void Remove(TEntity entity);

    /// <summary>
    /// Hard-deletes matching rows, bypassing soft-delete query filters.
    /// </summary>
    /// <param name="filter">The filter expression to select rows for deletion.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The number of rows affected.</returns>
    // Commits immediately and does NOT participate in an outer IUnitOfWork transaction.
    Task<int> BatchHardDeleteAsync(
        Expression<Func<TEntity, bool>> filter,
        CancellationToken ct = default);
}
