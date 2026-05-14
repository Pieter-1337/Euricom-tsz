using Tsz.Infrastructure.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Tsz.Infrastructure.Persistence;

public sealed class EfCoreUnitOfWork<TContext>(TContext context) : IUnitOfWork
    where TContext : DbContext
{
    public IRepository<T> RepositoryFor<T>() where T : class, IEntityBase
        => new EfCoreRepository<TContext, T>(context);

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => context.SaveChangesAsync(ct);
}
