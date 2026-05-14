namespace Tsz.Infrastructure.Abstractions;

public interface IUnitOfWork
{
    IRepository<T> RepositoryFor<T>() where T : class, IEntityBase;

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
