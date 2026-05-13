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

    public IQueryable<TEntity> GetAll(Expression<Func<TEntity, bool>>? filter = null)
        => filter is null ? _dbSet : _dbSet.Where(filter);

    public async Task<IEnumerable<TEntity>> GetAllAsListAsync(
        Expression<Func<TEntity, bool>>? filter = null,
        CancellationToken ct = default)
        => await GetAll(filter).ToListAsync(ct);

    public async Task<IEnumerable<TDto>> GetAllAsDtosAsync<TDto>(
        Expression<Func<TEntity, bool>>? filter = null,
        CancellationToken ct = default)
        where TDto : class, IEntityDto<TEntity, TDto>
        => await GetAll(filter).Select(TDto.Project).ToListAsync(ct);

    public async Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> filter,
        CancellationToken ct = default)
        => await _dbSet.FirstOrDefaultAsync(filter, ct);

    public async Task<TDto?> FirstOrDefaultAsDtoAsync<TDto>(
        Expression<Func<TEntity, bool>> filter,
        CancellationToken ct = default)
        where TDto : class, IEntityDto<TEntity, TDto>
        => await GetAll(filter).Select(TDto.Project).FirstOrDefaultAsync(ct);

    public async Task<TDto?> FirstOrDefaultAsDtoAsync<TDto, TNavigation>(
        Expression<Func<TEntity, bool>> filter,
        Expression<Func<TEntity, TNavigation>> navigation,
        CancellationToken ct = default)
        where TNavigation : class
        where TDto : class, IEntityDto<TNavigation, TDto>
        => await GetAll(filter)
            .Select(navigation)
            .Select(TDto.Project)
            .FirstOrDefaultAsync(ct);

    public async Task<TResult?> FirstOrDefaultWithProjectionAsync<TResult>(
        Expression<Func<TEntity, bool>> filter,
        Expression<Func<TEntity, TResult>> projection,
        CancellationToken ct = default)
        => await GetAll(filter).Select(projection).FirstOrDefaultAsync(ct);

    public async Task<TEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _dbSet.FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
        => await _dbSet.AnyAsync(e => e.Id == id, ct);

    public async Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> filter, CancellationToken ct = default)
        => await _dbSet.AnyAsync(filter, ct);

    public void Add(TEntity entity) => _dbSet.Add(entity);

    public void Remove(TEntity entity) => _dbSet.Remove(entity);
}
