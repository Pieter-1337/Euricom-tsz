using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;
using Tsz.Modules.Customers.Domain.Customers;
using Tsz.Modules.Customers.Features;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Common.Pagination;
using Tsz.Api.Tests.Builders;
using SortDir = Tsz.Infrastructure.Common.Pagination.SortDirection;

namespace Tsz.Api.Tests.Modules.Customers.Features;

/// <summary>
/// Tests drive the handler end-to-end. The stub repository wraps an EF Core
/// in-memory DbContext so ToKeysetPageAsync (which calls EF async methods) works.
/// </summary>
public class GetCustomersPagedHandlerTests : IDisposable
{
    private readonly CustomerTestDbContext _db;

    public GetCustomersPagedHandlerTests()
    {
        var options = new DbContextOptionsBuilder<CustomerTestDbContext>()
            .UseInMemoryDatabase("GetCustomersPagedHandlerTests_" + Guid.NewGuid())
            .Options;
        _db = new CustomerTestDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    // ── helpers ──────────────────────────────────────────────────────────────

    private GetCustomersPagedHandler BuildHandler(IEnumerable<Customer>? seed = null)
    {
        if (seed is not null)
        {
            _db.Customers.AddRange(seed);
            _db.SaveChanges();
        }
        var repo = new InMemoryCustomerRepository(_db);
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<Customer>()).Returns(repo);
        return new GetCustomersPagedHandler(uow.Object);
    }

    private static GetCustomersPagedQuery PagedQuery(
        string? search = null,
        string? sortBy = null,
        SortDir sortDir = SortDir.Asc,
        int pageSize = 50,
        string? cursor = null,
        bool deletedOnly = false)
        => new(search, sortBy, sortDir, pageSize, cursor, deletedOnly);

    // ── tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EmptySource_ReturnsEmptyPage()
    {
        var handler = BuildHandler();

        var page = await handler.HandleAsync(PagedQuery());

        page.Items.ShouldBeEmpty();
        page.Total.ShouldBe(0);
        page.NextCursor.ShouldBeNull();
    }

    [Fact]
    public async Task FirstPage_SmallDataset_ReturnsAllItems()
    {
        var customers = Enumerable.Range(1, 5)
            .Select(i => CustomerBuilder.Build(i))
            .ToList();
        var handler = BuildHandler(customers);

        var page = await handler.HandleAsync(PagedQuery(pageSize: 10));

        page.Items.Count.ShouldBe(5);
        page.Total.ShouldBe(5);
        page.NextCursor.ShouldBeNull();
    }

    [Fact]
    public async Task CursorHandoff_SecondPageContainsNextItems()
    {
        var customers = Enumerable.Range(1, 15)
            .Select(i => CustomerBuilder.Build(i))
            .ToList();
        var handler = BuildHandler(customers);

        var firstPage = await handler.HandleAsync(PagedQuery(pageSize: 10));
        firstPage.Items.Count.ShouldBe(10);
        firstPage.NextCursor.ShouldNotBeNull();
        firstPage.Total.ShouldBe(15);

        var secondPage = await handler.HandleAsync(PagedQuery(pageSize: 10, cursor: firstPage.NextCursor));
        secondPage.Items.Count.ShouldBe(5);
        secondPage.NextCursor.ShouldBeNull();
        secondPage.Total.ShouldBe(15);

        var allIds = firstPage.Items.Select(c => c.Id)
            .Concat(secondPage.Items.Select(c => c.Id))
            .ToList();
        allIds.Distinct().Count().ShouldBe(15);
    }

    [Fact]
    public async Task Search_FiltersByName()
    {
        var acme = CustomerBuilder.Build(1).WithName("Acme Corp");
        var ajax = CustomerBuilder.Build(2).WithName("Ajax Ltd");
        var beta = CustomerBuilder.Build(3).WithName("Beta Inc");
        var handler = BuildHandler([acme, ajax, beta]);

        var page = await handler.HandleAsync(PagedQuery(search: "corp"));

        page.Items.Count.ShouldBe(1);
        page.Items[0].Name.ShouldBe("Acme Corp");
    }

    [Fact]
    public async Task Search_FiltersByContactEmail()
    {
        var acme = CustomerBuilder.Build(1).WithName("Acme").WithContact("billing@acme.com");
        var beta = CustomerBuilder.Build(2).WithName("Beta").WithContact("info@beta.com");
        var handler = BuildHandler([acme, beta]);

        var page = await handler.HandleAsync(PagedQuery(search: "billing@"));

        page.Items.Count.ShouldBe(1);
        page.Items[0].Name.ShouldBe("Acme");
    }

    [Fact]
    public async Task Search_FiltersByCity()
    {
        var brussels = CustomerBuilder.Build(1).WithName("Brussels Co").WithAddress(city: "Brussels");
        var antwerp = CustomerBuilder.Build(2).WithName("Antwerp Co").WithAddress(city: "Antwerp");
        var handler = BuildHandler([brussels, antwerp]);

        var page = await handler.HandleAsync(PagedQuery(search: "brussels"));

        page.Items.Count.ShouldBe(1);
        page.Items[0].Name.ShouldBe("Brussels Co");
    }

    [Fact]
    public async Task Sort_ByNumberAsc_OrderIsCorrect()
    {
        var c1 = CustomerBuilder.Build(10);
        var c2 = CustomerBuilder.Build(2);
        var c3 = CustomerBuilder.Build(7);
        var handler = BuildHandler([c1, c2, c3]);

        var page = await handler.HandleAsync(PagedQuery(sortBy: "number", sortDir: SortDir.Asc));

        page.Items[0].Number.ShouldBe(2);
        page.Items[1].Number.ShouldBe(7);
        page.Items[2].Number.ShouldBe(10);
    }

    [Fact]
    public async Task Sort_ByNameDesc_OrderIsCorrect()
    {
        var a = CustomerBuilder.Build(1).WithName("Alpha");
        var b = CustomerBuilder.Build(2).WithName("Bravo");
        var c = CustomerBuilder.Build(3).WithName("Charlie");
        var handler = BuildHandler([a, b, c]);

        var page = await handler.HandleAsync(PagedQuery(sortBy: "name", sortDir: SortDir.Desc));

        page.Items[0].Name.ShouldBe("Charlie");
        page.Items[1].Name.ShouldBe("Bravo");
        page.Items[2].Name.ShouldBe("Alpha");
    }

    [Fact]
    public async Task TiebreakerById_StableOrder_WhenSortValuesEqual()
    {
        var id1 = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var id2 = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var c1 = CustomerBuilder.Build(1).WithName("Same").WithId(id1);
        var c2 = CustomerBuilder.Build(2).WithName("Same").WithId(id2);
        var handler = BuildHandler([c2, c1]);

        var page = await handler.HandleAsync(PagedQuery(sortBy: "name", sortDir: SortDir.Asc));

        page.Items[0].Id.ShouldBe(id1);
        page.Items[1].Id.ShouldBe(id2);
    }

    [Fact]
    public async Task SoftDeleted_HiddenByDefault()
    {
        var active = CustomerBuilder.Build(1).WithName("Active");
        var deleted = CustomerBuilder.Build(2).WithName("Deleted").SoftDeleted();
        var handler = BuildHandler([active, deleted]);

        var page = await handler.HandleAsync(PagedQuery(deletedOnly: false));

        page.Items.Count.ShouldBe(1);
        page.Items[0].Name.ShouldBe("Active");
        page.Total.ShouldBe(1);
    }

    [Fact]
    public async Task SoftDeleted_OnlyShownWithDeletedOnly()
    {
        var active = CustomerBuilder.Build(1).WithName("Active");
        var deleted = CustomerBuilder.Build(2).WithName("Deleted").SoftDeleted();
        var handler = BuildHandler([active, deleted]);

        var page = await handler.HandleAsync(PagedQuery(deletedOnly: true));

        page.Items.Count.ShouldBe(1);
        page.Items[0].Name.ShouldBe("Deleted");
        page.Total.ShouldBe(1);
    }

    [Fact]
    public async Task Total_RespectsSearchFilter_IgnoresCursor()
    {
        var customers = Enumerable.Range(1, 20)
            .Select(i => CustomerBuilder.Build(i).WithName($"Acme{i:D2}"))
            .ToList<Customer>();
        customers.AddRange(Enumerable.Range(21, 5)
            .Select(i => CustomerBuilder.Build(i).WithName($"Beta{i:D2}")));
        var handler = BuildHandler(customers);

        var firstPage = await handler.HandleAsync(PagedQuery(search: "acme", pageSize: 5));
        firstPage.Total.ShouldBe(20);

        var secondPage = await handler.HandleAsync(PagedQuery(search: "acme", pageSize: 5, cursor: firstPage.NextCursor));
        secondPage.Total.ShouldBe(20);
    }

    // ── in-memory EF Core context and repository ──────────────────────────────

    private sealed class CustomerTestDbContext(DbContextOptions<CustomerTestDbContext> options) : DbContext(options)
    {
        public DbSet<Customer> Customers => Set<Customer>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Customer>(b =>
            {
                b.HasKey(c => c.Id);
                b.Property(c => c.Name).IsRequired();
                b.OwnsOne(c => c.Address);
                b.OwnsOne(c => c.ContactPerson, cp =>
                {
                    cp.Property(p => p.Email).IsRequired();
                });
                b.HasQueryFilter("SoftDelete", c => c.DeletedAt == null);
            });
        }
    }

    private sealed class InMemoryCustomerRepository(CustomerTestDbContext db) : IRepository<Customer>
    {
        public IQueryable<Customer> GetAll(
            Expression<Func<Customer, bool>>? filter = null,
            bool ignoreQueryFilters = false)
        {
            IQueryable<Customer> q = ignoreQueryFilters
                ? db.Customers.IgnoreQueryFilters()
                : db.Customers;
            if (filter is not null)
                q = q.Where(filter);
            return q;
        }

        public Task<KeysetPage<TDto>> GetPagedAsync<TDto>(
            KeysetQueryOptions options,
            SortMap<Customer> sortMap,
            SearchableField<Customer>[] searchableFields,
            Expression<Func<Customer, TDto>> projection,
            Expression<Func<Customer, bool>>? filter = null,
            CancellationToken ct = default,
            bool ignoreQueryFilters = false)
        {
            IQueryable<Customer> query = db.Customers;
            if (ignoreQueryFilters)
                query = query.IgnoreQueryFilters();
            if (options.DeletedOnly)
                query = query.IgnoreQueryFilters(new[] { "SoftDelete" }).Where(c => c.DeletedAt != null);
            if (filter is not null)
                query = query.Where(filter);
            return query.ToKeysetPageAsync(options, sortMap, searchableFields, projection, ct);
        }

        public Task<IEnumerable<Customer>> GetAllAsListAsync(Expression<Func<Customer, bool>>? filter = null, CancellationToken ct = default, bool ignoreQueryFilters = false) => throw new NotImplementedException();
        public Task<IEnumerable<TDto>> GetAllAsDtosAsync<TDto>(Expression<Func<Customer, bool>>? filter = null, CancellationToken ct = default, bool ignoreQueryFilters = false) where TDto : class, IEntityDto<Customer, TDto> => throw new NotImplementedException();
        public Task<Customer?> FirstOrDefaultAsync(Expression<Func<Customer, bool>> filter, CancellationToken ct = default, bool ignoreQueryFilters = false) => throw new NotImplementedException();
        public Task<TDto?> FirstOrDefaultAsDtoAsync<TDto>(Expression<Func<Customer, bool>> filter, CancellationToken ct = default, bool ignoreQueryFilters = false) where TDto : class, IEntityDto<Customer, TDto> => throw new NotImplementedException();
        public Task<TDto?> FirstOrDefaultAsDtoAsync<TDto, TNavigation>(Expression<Func<Customer, bool>> filter, Expression<Func<Customer, TNavigation>> navigation, CancellationToken ct = default, bool ignoreQueryFilters = false) where TNavigation : class where TDto : class, IEntityDto<TNavigation, TDto> => throw new NotImplementedException();
        public Task<TResult?> FirstOrDefaultWithProjectionAsync<TResult>(Expression<Func<Customer, bool>> filter, Expression<Func<Customer, TResult>> projection, CancellationToken ct = default, bool ignoreQueryFilters = false) => throw new NotImplementedException();
        public Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct = default, bool ignoreQueryFilters = false) => throw new NotImplementedException();
        public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default, bool ignoreQueryFilters = false) => throw new NotImplementedException();
        public Task<bool> ExistsAsync(Expression<Func<Customer, bool>> filter, CancellationToken ct = default, bool ignoreQueryFilters = false) => throw new NotImplementedException();
        public void Add(Customer entity) => throw new NotImplementedException();
        public void Remove(Customer entity) => throw new NotImplementedException();
        public Task<int> BatchHardDeleteAsync(Expression<Func<Customer, bool>> filter, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
