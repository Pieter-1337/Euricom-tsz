using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;
using Tsz.Api.Tests.Builders;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Common.Pagination;
using Tsz.Modules.Contracts.Domain.Contracts;
using Tsz.Modules.Contracts.Features;
using Tsz.Modules.Customers.Contracts;
using Tsz.Modules.Customers.Contracts.Queries;
using SortDir = Tsz.Infrastructure.Common.Pagination.SortDirection;

namespace Tsz.Api.Tests.Modules.Contracts.Features;

public class GetContractsPagedHandlerTests : IDisposable
{
    private readonly ContractTestDbContext _db;

    public GetContractsPagedHandlerTests()
    {
        var options = new DbContextOptionsBuilder<ContractTestDbContext>()
            .UseInMemoryDatabase("GetContractsPagedHandlerTests_" + Guid.NewGuid())
            .Options;
        _db = new ContractTestDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    private GetContractsPagedHandler BuildHandler(
        IEnumerable<Contract>? seed = null,
        IReadOnlyList<Guid>? customerNameMatches = null)
    {
        if (seed is not null)
        {
            _db.Contracts.AddRange(seed);
            _db.SaveChanges();
        }
        var repo = new InMemoryContractRepository(_db);
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<Contract>()).Returns(repo);

        var customers = new Mock<ICustomersAccessModule>();
        customers
            .Setup(c => c.ExecuteQueryAsync(It.IsAny<FindCustomerIdsBySearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(customerNameMatches ?? Array.Empty<Guid>());

        return new GetContractsPagedHandler(uow.Object, customers.Object);
    }

    private static GetContractsPagedQuery PagedQuery(
        string? search = null,
        string? sortBy = null,
        SortDir sortDir = SortDir.Asc,
        int pageSize = 50,
        string? cursor = null,
        bool deletedOnly = false,
        DateOnly? activeOnDate = null,
        Guid? customerId = null)
        => new(search, sortBy, sortDir, pageSize, cursor, deletedOnly, activeOnDate, customerId);

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
    public async Task FirstPage_SmallDataset_ReturnsAllItemsWithZeroCounts()
    {
        var contracts = Enumerable.Range(1, 3).Select(i => ContractBuilder.Build(i)).ToList();
        var handler = BuildHandler(contracts);

        var page = await handler.HandleAsync(PagedQuery(pageSize: 10));

        page.Items.Count.ShouldBe(3);
        page.Total.ShouldBe(3);
        page.NextCursor.ShouldBeNull();
        page.Items.ShouldAllBe(c => c.ActiveTaskCount == 0 && c.ConsultantCount == 0);
    }

    [Fact]
    public async Task Pagination_SecondPageContainsNextItems()
    {
        var contracts = Enumerable.Range(1, 15).Select(i => ContractBuilder.Build(i)).ToList();
        var handler = BuildHandler(contracts);

        var firstPage = await handler.HandleAsync(PagedQuery(pageSize: 10));
        firstPage.Items.Count.ShouldBe(10);
        firstPage.Total.ShouldBe(15);
        firstPage.NextCursor.ShouldNotBeNull();

        var secondPage = await handler.HandleAsync(PagedQuery(pageSize: 10, cursor: firstPage.NextCursor));
        secondPage.Items.Count.ShouldBe(5);
        secondPage.Total.ShouldBe(15);
        secondPage.NextCursor.ShouldBeNull();
    }

    [Fact]
    public async Task Search_MatchesSubject_CaseInsensitive()
    {
        var a = ContractBuilder.Build(1).WithSubject("Engagement Alpha");
        var b = ContractBuilder.Build(2).WithSubject("Engagement Beta");
        var c = ContractBuilder.Build(3).WithSubject("Other");
        var handler = BuildHandler([a, b, c]);

        var page = await handler.HandleAsync(PagedQuery(search: "engagement"));

        page.Items.Count.ShouldBe(2);
        page.Items.ShouldAllBe(x => x.Subject.Contains("Engagement"));
    }

    [Fact]
    public async Task Search_MatchesCustomerName_ViaCustomerIdLookup()
    {
        var matchedCustomerId = Guid.NewGuid();
        var a = ContractBuilder.Build(1, customerId: matchedCustomerId).WithSubject("Unrelated");
        var b = ContractBuilder.Build(2).WithSubject("Other");
        var handler = BuildHandler([a, b], customerNameMatches: [matchedCustomerId]);

        var page = await handler.HandleAsync(PagedQuery(search: "acme"));

        page.Items.Count.ShouldBe(1);
        page.Items[0].CustomerId.ShouldBe(matchedCustomerId);
    }

    [Fact]
    public async Task ActiveOnDate_FilterIncludesContractsWithNullEnd()
    {
        var openEnded = ContractBuilder.Build(1).WithPeriod(new DateOnly(2026, 1, 1), end: null);
        var ended = ContractBuilder.Build(2).WithPeriod(new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31));
        var future = ContractBuilder.Build(3).WithPeriod(new DateOnly(2026, 6, 1), null);
        var handler = BuildHandler([openEnded, ended, future]);

        var page = await handler.HandleAsync(PagedQuery(activeOnDate: new DateOnly(2026, 5, 1)));

        page.Items.Count.ShouldBe(1);
        page.Items[0].Number.ShouldBe(1);
    }

    [Fact]
    public async Task ActiveOnDate_InclusiveOnBothEnds()
    {
        var contract = ContractBuilder.Build(1).WithPeriod(new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31));
        var handler = BuildHandler([contract]);

        var onStart = await handler.HandleAsync(PagedQuery(activeOnDate: new DateOnly(2026, 1, 1)));
        onStart.Items.Count.ShouldBe(1);

        var onEnd = await handler.HandleAsync(PagedQuery(activeOnDate: new DateOnly(2026, 3, 31)));
        onEnd.Items.Count.ShouldBe(1);

        var dayAfterEnd = await handler.HandleAsync(PagedQuery(activeOnDate: new DateOnly(2026, 4, 1)));
        dayAfterEnd.Items.ShouldBeEmpty();

        var dayBeforeStart = await handler.HandleAsync(PagedQuery(activeOnDate: new DateOnly(2025, 12, 31)));
        dayBeforeStart.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task CustomerId_FilterReturnsOnlyMatchingContracts()
    {
        var target = Guid.NewGuid();
        var other = Guid.NewGuid();
        var matching = ContractBuilder.Build(1, customerId: target);
        var nonMatching = ContractBuilder.Build(2, customerId: other);
        var handler = BuildHandler([matching, nonMatching]);

        var page = await handler.HandleAsync(PagedQuery(customerId: target));

        page.Items.Count.ShouldBe(1);
        page.Items[0].CustomerId.ShouldBe(target);
    }

    [Fact]
    public async Task SoftDeleted_HiddenByDefault()
    {
        var active = ContractBuilder.Build(1).WithSubject("Active");
        var deleted = ContractBuilder.Build(2).WithSubject("Deleted").SoftDeleted();
        var handler = BuildHandler([active, deleted]);

        var page = await handler.HandleAsync(PagedQuery());

        page.Items.Count.ShouldBe(1);
        page.Items[0].Subject.ShouldBe("Active");
        page.Total.ShouldBe(1);
    }

    [Fact]
    public async Task CombinedFilters_AllApplied()
    {
        var customerA = Guid.NewGuid();
        var customerB = Guid.NewGuid();

        var inA_subjectAlpha = ContractBuilder.Build(1, customerId: customerA)
            .WithSubject("Alpha Engagement")
            .WithPeriod(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        var inA_subjectBeta = ContractBuilder.Build(2, customerId: customerA)
            .WithSubject("Beta Project")
            .WithPeriod(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        var inB_subjectAlpha = ContractBuilder.Build(3, customerId: customerB)
            .WithSubject("Alpha Engagement")
            .WithPeriod(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        var handler = BuildHandler([inA_subjectAlpha, inA_subjectBeta, inB_subjectAlpha]);

        var page = await handler.HandleAsync(PagedQuery(
            search: "alpha",
            customerId: customerA,
            activeOnDate: new DateOnly(2026, 6, 1)));

        page.Items.Count.ShouldBe(1);
        page.Items[0].Number.ShouldBe(1);
    }

    private sealed class ContractTestDbContext(DbContextOptions<ContractTestDbContext> options) : DbContext(options)
    {
        public DbSet<Contract> Contracts => Set<Contract>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new ContractConfiguration());
        }
    }

    private sealed class InMemoryContractRepository(ContractTestDbContext db) : IRepository<Contract>
    {
        public IQueryable<Contract> GetAll(
            Expression<Func<Contract, bool>>? filter = null,
            bool ignoreQueryFilters = false)
        {
            IQueryable<Contract> q = ignoreQueryFilters
                ? db.Contracts.IgnoreQueryFilters()
                : db.Contracts;
            if (filter is not null)
                q = q.Where(filter);
            return q;
        }

        public Task<KeysetPage<TDto>> GetPagedAsync<TDto>(
            KeysetQueryOptions options,
            SortMap<Contract> sortMap,
            SearchableField<Contract>[] searchableFields,
            Expression<Func<Contract, TDto>> projection,
            Expression<Func<Contract, bool>>? filter = null,
            CancellationToken ct = default,
            bool ignoreQueryFilters = false)
        {
            IQueryable<Contract> query = db.Contracts;
            if (ignoreQueryFilters)
                query = query.IgnoreQueryFilters();
            if (options.DeletedOnly)
                query = query.IgnoreQueryFilters(new[] { "SoftDelete" }).Where(c => c.DeletedAt != null);
            if (filter is not null)
                query = query.Where(filter);
            return query.ToKeysetPageAsync(options, sortMap, searchableFields, projection, ct);
        }

        public Task<IEnumerable<Contract>> GetAllAsListAsync(Expression<Func<Contract, bool>>? filter = null, CancellationToken ct = default, bool ignoreQueryFilters = false) => throw new NotImplementedException();
        public Task<IEnumerable<TDto>> GetAllAsDtosAsync<TDto>(Expression<Func<Contract, bool>>? filter = null, CancellationToken ct = default, bool ignoreQueryFilters = false) where TDto : class, IEntityDto<Contract, TDto> => throw new NotImplementedException();
        public Task<Contract?> FirstOrDefaultAsync(Expression<Func<Contract, bool>> filter, CancellationToken ct = default, bool ignoreQueryFilters = false) => throw new NotImplementedException();
        public Task<TDto?> FirstOrDefaultAsDtoAsync<TDto>(Expression<Func<Contract, bool>> filter, CancellationToken ct = default, bool ignoreQueryFilters = false) where TDto : class, IEntityDto<Contract, TDto> => throw new NotImplementedException();
        public Task<TDto?> FirstOrDefaultAsDtoAsync<TDto, TNavigation>(Expression<Func<Contract, bool>> filter, Expression<Func<Contract, TNavigation>> navigation, CancellationToken ct = default, bool ignoreQueryFilters = false) where TNavigation : class where TDto : class, IEntityDto<TNavigation, TDto> => throw new NotImplementedException();
        public Task<TResult?> FirstOrDefaultWithProjectionAsync<TResult>(Expression<Func<Contract, bool>> filter, Expression<Func<Contract, TResult>> projection, CancellationToken ct = default, bool ignoreQueryFilters = false) => throw new NotImplementedException();
        public Task<Contract?> GetByIdAsync(Guid id, CancellationToken ct = default, bool ignoreQueryFilters = false) => throw new NotImplementedException();
        public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default, bool ignoreQueryFilters = false) => throw new NotImplementedException();
        public Task<bool> ExistsAsync(Expression<Func<Contract, bool>> filter, CancellationToken ct = default, bool ignoreQueryFilters = false) => throw new NotImplementedException();
        public void Add(Contract entity) => throw new NotImplementedException();
        public void Remove(Contract entity) => throw new NotImplementedException();
        public Task<int> BatchHardDeleteAsync(Expression<Func<Contract, bool>> filter, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
