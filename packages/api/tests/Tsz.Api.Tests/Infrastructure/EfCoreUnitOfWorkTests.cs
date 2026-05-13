using Tsz.Api.Common.Persistence;
using Tsz.Api.Modules.Animals;
using Tsz.Api.Tests.Builders;
using Tsz.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Tsz.Api.Tests.Infrastructure;

public class EfCoreUnitOfWorkTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _ctx;
    private readonly EfCoreUnitOfWork<AppDbContext> _uow;

    public EfCoreUnitOfWorkTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _ctx = new AppDbContext(options);
        _ctx.Database.EnsureCreated();
        _uow = new EfCoreUnitOfWork<AppDbContext>(_ctx);
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
        _uow.RepositoryFor<Animal>().Add(AnimalBuilder.Build());
        await _uow.SaveChangesAsync();
        await _uow.CloseTransactionAsync(null);

        (await _ctx.Animals.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task SingleBeginRollback_DiscardsChanges()
    {
        await _uow.BeginTransactionAsync();
        _uow.RepositoryFor<Animal>().Add(AnimalBuilder.Build());
        await _uow.SaveChangesAsync();
        await _uow.CloseTransactionAsync(new Exception("boom"));

        (await _ctx.Animals.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task NestedBeginCommit_CommitsOnce()
    {
        await _uow.BeginTransactionAsync();
        _uow.RepositoryFor<Animal>().Add(AnimalBuilder.Build().WithName("Outer"));

        await _uow.BeginTransactionAsync();
        _uow.RepositoryFor<Animal>().Add(AnimalBuilder.Build().WithName("Inner"));
        await _uow.SaveChangesAsync();
        await _uow.CloseTransactionAsync(null);

        await _uow.SaveChangesAsync();
        await _uow.CloseTransactionAsync(null);

        (await _ctx.Animals.CountAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task InnerCloseWithException_PoisonsOuterCommit()
    {
        await _uow.BeginTransactionAsync();
        _uow.RepositoryFor<Animal>().Add(AnimalBuilder.Build().WithName("Outer"));
        await _uow.SaveChangesAsync();

        await _uow.BeginTransactionAsync();
        _uow.RepositoryFor<Animal>().Add(AnimalBuilder.Build().WithName("Inner"));
        await _uow.SaveChangesAsync();
        await _uow.CloseTransactionAsync(new Exception("inner failed"));

        await _uow.CloseTransactionAsync(null);

        (await _ctx.Animals.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task RollbackOnly_ResetsBetweenTransactions()
    {
        await _uow.BeginTransactionAsync();
        await _uow.BeginTransactionAsync();
        await _uow.CloseTransactionAsync(new Exception("poisoned"));
        await _uow.CloseTransactionAsync(null);

        await _uow.BeginTransactionAsync();
        _uow.RepositoryFor<Animal>().Add(AnimalBuilder.Build().WithName("AfterPoison"));
        await _uow.SaveChangesAsync();
        await _uow.CloseTransactionAsync(null);

        (await _ctx.Animals.CountAsync()).ShouldBe(1);
    }
}
