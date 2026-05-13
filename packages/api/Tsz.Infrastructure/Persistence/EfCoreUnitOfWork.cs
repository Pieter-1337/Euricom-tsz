using Tsz.Infrastructure.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Tsz.Infrastructure.Persistence;

public sealed class EfCoreUnitOfWork<TContext>(TContext context) : IUnitOfWork
    where TContext : DbContext
{
    private IDbContextTransaction? _transaction;
    private int _transactionDepth;
    private bool _rollbackOnly;

    public IRepository<T> RepositoryFor<T>() where T : class, IEntityBase
        => new EfCoreRepository<TContext, T>(context);

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => context.SaveChangesAsync(ct);

    public async Task BeginTransactionAsync(CancellationToken ct = default)
    {
        _transactionDepth++;
        if (_transactionDepth > 1) return;
        _rollbackOnly = false;
        _transaction = await context.Database.BeginTransactionAsync(ct);
    }

    public async Task CloseTransactionAsync(Exception? exception = null, CancellationToken ct = default)
    {
        if (_transaction is null) return;

        if (exception is not null) _rollbackOnly = true;

        _transactionDepth--;
        if (_transactionDepth > 0) return;

        try
        {
            if (_rollbackOnly)
                await _transaction.RollbackAsync(ct);
            else
                await _transaction.CommitAsync(ct);
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
            _transactionDepth = 0;
            _rollbackOnly = false;
        }
    }
}
