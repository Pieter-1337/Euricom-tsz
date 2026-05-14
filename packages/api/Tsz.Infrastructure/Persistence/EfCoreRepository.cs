using Tsz.Infrastructure.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Tsz.Infrastructure.Persistence;

public class EfCoreRepository<TContext, TEntity> : IRepository<TEntity>
    where TContext : DbContext
    where TEntity : class, IEntityBase
{
    private readonly TContext _context;
    private readonly DbSet<TEntity> _dbSet;

    public EfCoreRepository(TContext context)
    {
        _context = context;
        _dbSet = _context.Set<TEntity>();
    }

    public IQueryable<TEntity> GetAll(
        Expression<Func<TEntity, bool>>? filter = null,
        bool ignoreQueryFilters = false)
    {
        IQueryable<TEntity> query = _dbSet;
        if (ignoreQueryFilters) query = query.IgnoreQueryFilters();
        if (filter is not null) query = query.Where(filter);
        return query;
    }

    public async Task<IEnumerable<TEntity>> GetAllAsListAsync(
        Expression<Func<TEntity, bool>>? filter = null,
        CancellationToken ct = default,
        bool ignoreQueryFilters = false)
        => await GetAll(filter, ignoreQueryFilters).ToListAsync(ct);

    public async Task<IEnumerable<TDto>> GetAllAsDtosAsync<TDto>(
        Expression<Func<TEntity, bool>>? filter = null,
        CancellationToken ct = default,
        bool ignoreQueryFilters = false)
        where TDto : class, IEntityDto<TEntity, TDto>
        => await GetAll(filter, ignoreQueryFilters).Select(TDto.Project).ToListAsync(ct);

    public async Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> filter,
        CancellationToken ct = default,
        bool ignoreQueryFilters = false)
        => await GetAll(filter, ignoreQueryFilters).FirstOrDefaultAsync(ct);

    public async Task<TDto?> FirstOrDefaultAsDtoAsync<TDto>(
        Expression<Func<TEntity, bool>> filter,
        CancellationToken ct = default,
        bool ignoreQueryFilters = false)
        where TDto : class, IEntityDto<TEntity, TDto>
        => await GetAll(filter, ignoreQueryFilters).Select(TDto.Project).FirstOrDefaultAsync(ct);

    public async Task<TDto?> FirstOrDefaultAsDtoAsync<TDto, TNavigation>(
        Expression<Func<TEntity, bool>> filter,
        Expression<Func<TEntity, TNavigation>> navigation,
        CancellationToken ct = default,
        bool ignoreQueryFilters = false)
        where TNavigation : class
        where TDto : class, IEntityDto<TNavigation, TDto>
        => await GetAll(filter, ignoreQueryFilters)
            .Select(navigation)
            .Select(TDto.Project)
            .FirstOrDefaultAsync(ct);

    public async Task<TResult?> FirstOrDefaultWithProjectionAsync<TResult>(
        Expression<Func<TEntity, bool>> filter,
        Expression<Func<TEntity, TResult>> projection,
        CancellationToken ct = default,
        bool ignoreQueryFilters = false)
        => await GetAll(filter, ignoreQueryFilters).Select(projection).FirstOrDefaultAsync(ct);

    public Task<TEntity?> GetByIdAsync(
        Guid id,
        CancellationToken ct = default,
        bool ignoreQueryFilters = false)
        => FirstOrDefaultAsync(e => e.Id == id, ct, ignoreQueryFilters);

    public Task<bool> ExistsAsync(
        Guid id,
        CancellationToken ct = default,
        bool ignoreQueryFilters = false)
        => ExistsAsync(e => e.Id == id, ct, ignoreQueryFilters);

    public async Task<bool> ExistsAsync(
        Expression<Func<TEntity, bool>> filter,
        CancellationToken ct = default,
        bool ignoreQueryFilters = false)
        => await GetAll(filter, ignoreQueryFilters).AnyAsync(ct);

    public void Add(TEntity entity) => _dbSet.Add(entity);

    public void Remove(TEntity entity) => _dbSet.Remove(entity);

    public async Task<int> BatchHardDeleteAsync(
        Expression<Func<TEntity, bool>> filter,
        CancellationToken ct = default)
    {
        var query = GetAll(filter, ignoreQueryFilters: true);

        // ExecuteDeleteAsync is relational-only; InMemory (used by integration tests) needs a load+remove fallback.
        if (_context.Database.IsRelational())
            return await query.ExecuteDeleteAsync(ct);

        var rows = await query.ToListAsync(ct);
        if (rows.Count == 0) return 0;
        _dbSet.RemoveRange(rows);
        return await _context.SaveChangesAsync(ct);
    }
}
