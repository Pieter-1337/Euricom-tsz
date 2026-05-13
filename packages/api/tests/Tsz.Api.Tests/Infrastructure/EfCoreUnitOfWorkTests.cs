using Tsz.Api.Tests.Infrastructure.Fixtures;
using Tsz.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Tsz.Api.Tests.Infrastructure;

public class EfCoreUnitOfWorkTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestDbContext _ctx;
    private readonly EfCoreUnitOfWork<TestDbContext> _uow;

    public EfCoreUnitOfWorkTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(_connection)
            .Options;
        _ctx = new TestDbContext(options);
        _ctx.Database.EnsureCreated();
        _uow = new EfCoreUnitOfWork<TestDbContext>(_ctx);
    }

    public void Dispose()
    {
        _ctx.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task SingleBeginCommit_PersistsChanges()
    {
        await _uow.BeginTransactionAsync();
        _uow.RepositoryFor<TestEntity>().Add(TestEntity.Create("a"));
        await _uow.SaveChangesAsync();
        await _uow.CloseTransactionAsync(null);

        (await _ctx.TestEntities.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task SingleBeginRollback_DiscardsChanges()
    {
        await _uow.BeginTransactionAsync();
        _uow.RepositoryFor<TestEntity>().Add(TestEntity.Create("a"));
        await _uow.SaveChangesAsync();
        await _uow.CloseTransactionAsync(new Exception("boom"));

        (await _ctx.TestEntities.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task NestedBeginCommit_CommitsOnce()
    {
        await _uow.BeginTransactionAsync();
        _uow.RepositoryFor<TestEntity>().Add(TestEntity.Create("Outer"));

        await _uow.BeginTransactionAsync();
        _uow.RepositoryFor<TestEntity>().Add(TestEntity.Create("Inner"));
        await _uow.SaveChangesAsync();
        await _uow.CloseTransactionAsync(null);

        await _uow.SaveChangesAsync();
        await _uow.CloseTransactionAsync(null);

        (await _ctx.TestEntities.CountAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task InnerCloseWithException_PoisonsOuterCommit()
    {
        await _uow.BeginTransactionAsync();
        _uow.RepositoryFor<TestEntity>().Add(TestEntity.Create("Outer"));
        await _uow.SaveChangesAsync();

        await _uow.BeginTransactionAsync();
        _uow.RepositoryFor<TestEntity>().Add(TestEntity.Create("Inner"));
        await _uow.SaveChangesAsync();
        await _uow.CloseTransactionAsync(new Exception("inner failed"));

        await _uow.CloseTransactionAsync(null);

        (await _ctx.TestEntities.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task RollbackOnly_ResetsBetweenTransactions()
    {
        await _uow.BeginTransactionAsync();
        await _uow.BeginTransactionAsync();
        await _uow.CloseTransactionAsync(new Exception("poisoned"));
        await _uow.CloseTransactionAsync(null);

        await _uow.BeginTransactionAsync();
        _uow.RepositoryFor<TestEntity>().Add(TestEntity.Create("AfterPoison"));
        await _uow.SaveChangesAsync();
        await _uow.CloseTransactionAsync(null);

        (await _ctx.TestEntities.CountAsync()).ShouldBe(1);
    }
}
