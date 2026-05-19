using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;
using Tsz.Api.Modules.Users;
using Tsz.Api.Modules.Users.Features;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Common.Pagination;
using Tsz.Api.Tests.Builders;
using SortDir = Tsz.Infrastructure.Common.Pagination.SortDirection;

namespace Tsz.Api.Tests.Modules.Users.Features;

/// <summary>
/// Tests drive the handler end-to-end. The stub repository wraps an EF Core
/// in-memory DbContext so ToKeysetPageAsync (which calls EF async methods) works.
/// </summary>
public class GetUsersPagedHandlerTests : IDisposable
{
    private readonly UserTestDbContext _db;

    public GetUsersPagedHandlerTests()
    {
        var options = new DbContextOptionsBuilder<UserTestDbContext>()
            .UseInMemoryDatabase("GetUsersPagedHandlerTests_" + Guid.NewGuid())
            .Options;
        _db = new UserTestDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    // ── helpers ──────────────────────────────────────────────────────────────

    private GetUsersPagedHandler BuildHandler(IEnumerable<User>? seed = null)
    {
        if (seed is not null)
        {
            _db.Users.AddRange(seed);
            _db.SaveChanges();
        }
        var repo = new InMemoryUserRepository(_db);
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<User>()).Returns(repo);
        return new GetUsersPagedHandler(uow.Object);
    }

    private static GetUsersPagedQuery PagedQuery(
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
        var users = Enumerable.Range(1, 5)
            .Select(i => UserBuilder.Build().WithFirstName($"User{i:D2}"))
            .ToList();
        var handler = BuildHandler(users);

        var page = await handler.HandleAsync(PagedQuery(pageSize: 10));

        page.Items.Count.ShouldBe(5);
        page.Total.ShouldBe(5);
        page.NextCursor.ShouldBeNull();
    }

    [Fact]
    public async Task CursorHandoff_SecondPageContainsNextItems()
    {
        var users = Enumerable.Range(1, 15)
            .Select(i => UserBuilder.Build().WithFirstName($"User{i:D2}"))
            .ToList();
        var handler = BuildHandler(users);

        var firstPage = await handler.HandleAsync(PagedQuery(pageSize: 10));
        firstPage.Items.Count.ShouldBe(10);
        firstPage.NextCursor.ShouldNotBeNull();
        firstPage.Total.ShouldBe(15);

        var secondPage = await handler.HandleAsync(PagedQuery(pageSize: 10, cursor: firstPage.NextCursor));
        secondPage.Items.Count.ShouldBe(5);
        secondPage.NextCursor.ShouldBeNull();
        secondPage.Total.ShouldBe(15);

        var allIds = firstPage.Items.Select(u => u.Id)
            .Concat(secondPage.Items.Select(u => u.Id))
            .ToList();
        allIds.Distinct().Count().ShouldBe(15);
    }

    [Fact]
    public async Task Search_FiltersByFirstName()
    {
        var users = new[]
        {
            User.Create("Alice", "Smith", "alice@x.com", [UserRole.User]),
            User.Create("Bob", "Jones", "bob@x.com", [UserRole.User]),
            User.Create("Charlie", "Alison", "charlie@x.com", [UserRole.User]),
        };
        var handler = BuildHandler(users);

        var page = await handler.HandleAsync(PagedQuery(search: "ali"));

        // Matches "Alice" (firstName) and "Charlie Alison" (lastName contains "ali")
        page.Items.Count.ShouldBe(2);
        page.Total.ShouldBe(2);
    }

    [Fact]
    public async Task Search_FiltersByEmail()
    {
        var alice = User.Create("Alice", "Smith", "alice@example.com", [UserRole.User]);
        var bob = User.Create("Bob", "Jones", "bob@example.com", [UserRole.User]);
        var handler = BuildHandler([alice, bob]);

        var page = await handler.HandleAsync(PagedQuery(search: "alice@"));

        page.Items.Count.ShouldBe(1);
        page.Items[0].Email.ShouldBe("alice@example.com");
    }

    [Fact]
    public async Task Sort_ByEmailDesc_OrderIsCorrect()
    {
        var a = User.Create("A", "A", "aaa@example.com", [UserRole.User]);
        var b = User.Create("B", "B", "bbb@example.com", [UserRole.User]);
        var c = User.Create("C", "C", "ccc@example.com", [UserRole.User]);
        var handler = BuildHandler([c, a, b]);

        var page = await handler.HandleAsync(PagedQuery(sortBy: "email", sortDir: SortDir.Desc));

        page.Items[0].Email.ShouldBe("ccc@example.com");
        page.Items[1].Email.ShouldBe("bbb@example.com");
        page.Items[2].Email.ShouldBe("aaa@example.com");
    }

    [Fact]
    public async Task Sort_ByNameAsc_OrderIsCorrect()
    {
        var a = User.Create("Zara", "X", "z@x.com", [UserRole.User]);
        var b = User.Create("Adam", "Y", "a@x.com", [UserRole.User]);
        var handler = BuildHandler([a, b]);

        var page = await handler.HandleAsync(PagedQuery(sortBy: "name", sortDir: SortDir.Asc));

        page.Items[0].FirstName.ShouldBe("Adam");
        page.Items[1].FirstName.ShouldBe("Zara");
    }

    [Fact]
    public async Task TiebreakerById_StableOrder_WhenSortValuesEqual()
    {
        var id1 = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var id2 = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var u1 = User.Create("Same", "X", "x1@x.com", [UserRole.User]);
        u1.Id = id1;
        var u2 = User.Create("Same", "Y", "x2@x.com", [UserRole.User]);
        u2.Id = id2;
        var handler = BuildHandler([u2, u1]);

        var page = await handler.HandleAsync(PagedQuery(sortBy: "name", sortDir: SortDir.Asc));

        page.Items[0].Id.ShouldBe(id1);
        page.Items[1].Id.ShouldBe(id2);
    }

    [Fact]
    public async Task SoftDeleted_HiddenByDefault()
    {
        var active = UserBuilder.Build().WithFirstName("Active");
        var deleted = UserBuilder.Build().WithFirstName("Deleted").SoftDeleted();
        var handler = BuildHandler([active, deleted]);

        var page = await handler.HandleAsync(PagedQuery(deletedOnly: false));

        page.Items.Count.ShouldBe(1);
        page.Items[0].FirstName.ShouldBe("Active");
        page.Total.ShouldBe(1);
    }

    [Fact]
    public async Task SoftDeleted_OnlyShownWithDeletedOnly()
    {
        var active = UserBuilder.Build().WithFirstName("Active");
        var deleted = UserBuilder.Build().WithFirstName("Deleted").SoftDeleted();
        var handler = BuildHandler([active, deleted]);

        var page = await handler.HandleAsync(PagedQuery(deletedOnly: true));

        page.Items.Count.ShouldBe(1);
        page.Items[0].FirstName.ShouldBe("Deleted");
        page.Total.ShouldBe(1);
    }

    [Fact]
    public async Task Total_RespectsSearchFilter_IgnoresCursor()
    {
        var users = Enumerable.Range(1, 20)
            .Select(i => User.Create($"Alice{i:D2}", "X", $"alice{i:D2}@x.com", [UserRole.User]))
            .ToList<User>();
        users.AddRange(Enumerable.Range(1, 5)
            .Select(i => User.Create($"Bob{i:D2}", "Y", $"bob{i:D2}@x.com", [UserRole.User])));
        var handler = BuildHandler(users);

        var firstPage = await handler.HandleAsync(PagedQuery(search: "alice", pageSize: 5));
        firstPage.Total.ShouldBe(20);

        var secondPage = await handler.HandleAsync(PagedQuery(search: "alice", pageSize: 5, cursor: firstPage.NextCursor));
        secondPage.Total.ShouldBe(20);
    }

    // ── in-memory EF Core context and repository ──────────────────────────────

    private sealed class UserTestDbContext(DbContextOptions<UserTestDbContext> options) : DbContext(options)
    {
        public DbSet<User> Users => Set<User>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>(b =>
            {
                b.HasKey(u => u.Id);
                b.Property(u => u.Email).IsRequired();
                b.Property(u => u.FirstName).IsRequired();
                b.Property(u => u.LastName).IsRequired();
                b.OwnsMany(u => u.RoleAssignments, ra =>
                {
                    ra.WithOwner().HasForeignKey("UserId");
                    ra.Property(r => r.Role).HasConversion<string>();
                    ra.HasKey("UserId", nameof(UserRoleAssignment.Role));
                });
                b.HasQueryFilter(u => u.DeletedAt == null);
            });
        }
    }

    private sealed class InMemoryUserRepository(UserTestDbContext db) : IRepository<User>
    {
        public IQueryable<User> GetAll(
            Expression<Func<User, bool>>? filter = null,
            bool ignoreQueryFilters = false)
        {
            IQueryable<User> q = ignoreQueryFilters
                ? db.Users.IgnoreQueryFilters()
                : db.Users;
            if (filter is not null)
                q = q.Where(filter);
            return q;
        }

        public Task<KeysetPage<TDto>> GetPagedAsync<TDto>(
            KeysetQueryOptions options,
            SortMap<User> sortMap,
            Expression<Func<User, string>>[] searchableColumns,
            Expression<Func<User, TDto>> projection,
            Expression<Func<User, bool>>? filter = null,
            CancellationToken ct = default,
            bool ignoreQueryFilters = false)
            => GetAll(filter, ignoreQueryFilters)
                .ToKeysetPageAsync(options, sortMap, searchableColumns, projection, ct);

        public Task<IEnumerable<User>> GetAllAsListAsync(Expression<Func<User, bool>>? filter = null, CancellationToken ct = default, bool ignoreQueryFilters = false) => throw new NotImplementedException();
        public Task<IEnumerable<TDto>> GetAllAsDtosAsync<TDto>(Expression<Func<User, bool>>? filter = null, CancellationToken ct = default, bool ignoreQueryFilters = false) where TDto : class, IEntityDto<User, TDto> => throw new NotImplementedException();
        public Task<User?> FirstOrDefaultAsync(Expression<Func<User, bool>> filter, CancellationToken ct = default, bool ignoreQueryFilters = false) => throw new NotImplementedException();
        public Task<TDto?> FirstOrDefaultAsDtoAsync<TDto>(Expression<Func<User, bool>> filter, CancellationToken ct = default, bool ignoreQueryFilters = false) where TDto : class, IEntityDto<User, TDto> => throw new NotImplementedException();
        public Task<TDto?> FirstOrDefaultAsDtoAsync<TDto, TNavigation>(Expression<Func<User, bool>> filter, Expression<Func<User, TNavigation>> navigation, CancellationToken ct = default, bool ignoreQueryFilters = false) where TNavigation : class where TDto : class, IEntityDto<TNavigation, TDto> => throw new NotImplementedException();
        public Task<TResult?> FirstOrDefaultWithProjectionAsync<TResult>(Expression<Func<User, bool>> filter, Expression<Func<User, TResult>> projection, CancellationToken ct = default, bool ignoreQueryFilters = false) => throw new NotImplementedException();
        public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default, bool ignoreQueryFilters = false) => throw new NotImplementedException();
        public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default, bool ignoreQueryFilters = false) => throw new NotImplementedException();
        public Task<bool> ExistsAsync(Expression<Func<User, bool>> filter, CancellationToken ct = default, bool ignoreQueryFilters = false) => throw new NotImplementedException();
        public void Add(User entity) => throw new NotImplementedException();
        public void Remove(User entity) => throw new NotImplementedException();
        public Task<int> BatchHardDeleteAsync(Expression<Func<User, bool>> filter, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
