using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Tsz.Infrastructure.Common.Pagination;
using PaginationSortDir = Tsz.Infrastructure.Common.Pagination.SortDirection;

namespace Tsz.Api.Tests.Common.Pagination;

// ── Fixture entity ────────────────────────────────────────────────────────────

internal sealed class Widget
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Score { get; set; }
}

internal sealed record WidgetDto(Guid Id, string Name, int Score);

internal sealed class WidgetDbContext(DbContextOptions<WidgetDbContext> options) : DbContext(options)
{
    public DbSet<Widget> Widgets => Set<Widget>();
}

// ── Sort map ──────────────────────────────────────────────────────────────────

internal static class WidgetSortMap
{
    public static readonly SortMap<Widget> Value = new(
        new SortColumn<Widget>("name", (Expression<Func<Widget, string>>)(w => w.Name), typeof(string)),
        new SortColumn<Widget>("score", (Expression<Func<Widget, int>>)(w => w.Score), typeof(int)));

    public static readonly Expression<Func<Widget, WidgetDto>> Projection =
        w => new WidgetDto(w.Id, w.Name, w.Score);

    public static readonly Expression<Func<Widget, string>>[] SearchColumns =
        [(Expression<Func<Widget, string>>)(w => w.Name)];
}

// ── Tests ─────────────────────────────────────────────────────────────────────

public class KeysetQueryableExtensionsTests : IDisposable
{
    private readonly WidgetDbContext _ctx;

    public KeysetQueryableExtensionsTests()
    {
        var opts = new DbContextOptionsBuilder<WidgetDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _ctx = new WidgetDbContext(opts);
    }

    public void Dispose() => _ctx.Dispose();

    private async Task SeedAsync(IEnumerable<Widget> widgets)
    {
        _ctx.Widgets.AddRange(widgets);
        await _ctx.SaveChangesAsync();
    }

    private static KeysetQueryOptions DefaultOpts(int pageSize = 50, string? cursor = null,
        string? search = null, string? sortBy = null, PaginationSortDir dir = PaginationSortDir.Asc)
        => new(Search: search, SortBy: sortBy, SortDir: dir, PageSize: pageSize,
               Cursor: cursor, IncludeDeleted: false);

    // ── Empty source ──────────────────────────────────────────────────────────

    [Fact]
    public async Task EmptySource_ReturnsEmptyPage()
    {
        var page = await _ctx.Widgets.ToKeysetPageAsync(
            DefaultOpts(), WidgetSortMap.Value, WidgetSortMap.SearchColumns, WidgetSortMap.Projection);

        page.Items.ShouldBeEmpty();
        page.NextCursor.ShouldBeNull();
        page.Total.ShouldBe(0);
    }

    // ── Single page (items ≤ pageSize) ────────────────────────────────────────

    [Fact]
    public async Task SinglePage_NoNextCursor()
    {
        await SeedAsync(
        [
            new Widget { Id = Guid.NewGuid(), Name = "Alpha", Score = 1 },
            new Widget { Id = Guid.NewGuid(), Name = "Beta",  Score = 2 },
        ]);

        var page = await _ctx.Widgets.ToKeysetPageAsync(
            DefaultOpts(pageSize: 50), WidgetSortMap.Value, WidgetSortMap.SearchColumns, WidgetSortMap.Projection);

        page.Items.Count.ShouldBe(2);
        page.NextCursor.ShouldBeNull();
        page.Total.ShouldBe(2);
    }

    // ── Multi-page cursor handoff ─────────────────────────────────────────────

    [Fact]
    public async Task MultiPage_CursorHandoff()
    {
        var widgets = Enumerable.Range(1, 5)
            .Select(i => new Widget { Id = Guid.NewGuid(), Name = $"Item{i:D2}", Score = i })
            .ToList();
        await SeedAsync(widgets);

        // Page 1: pageSize = 2.
        var page1 = await _ctx.Widgets.ToKeysetPageAsync(
            DefaultOpts(pageSize: 2), WidgetSortMap.Value, WidgetSortMap.SearchColumns, WidgetSortMap.Projection);

        page1.Items.Count.ShouldBe(2);
        page1.NextCursor.ShouldNotBeNull();
        page1.Total.ShouldBe(5);
        page1.Items[0].Name.ShouldBe("Item01");
        page1.Items[1].Name.ShouldBe("Item02");

        // Page 2: using cursor from page 1.
        var page2 = await _ctx.Widgets.ToKeysetPageAsync(
            DefaultOpts(pageSize: 2, cursor: page1.NextCursor),
            WidgetSortMap.Value, WidgetSortMap.SearchColumns, WidgetSortMap.Projection);

        page2.Items.Count.ShouldBe(2);
        page2.NextCursor.ShouldNotBeNull();
        page2.Items[0].Name.ShouldBe("Item03");
        page2.Items[1].Name.ShouldBe("Item04");

        // Page 3: last page.
        var page3 = await _ctx.Widgets.ToKeysetPageAsync(
            DefaultOpts(pageSize: 2, cursor: page2.NextCursor),
            WidgetSortMap.Value, WidgetSortMap.SearchColumns, WidgetSortMap.Projection);

        page3.Items.Count.ShouldBe(1);
        page3.NextCursor.ShouldBeNull();
        page3.Items[0].Name.ShouldBe("Item05");
    }

    // ── Search filter ─────────────────────────────────────────────────────────

    [Fact]
    public async Task SearchFilter_FiltersResults()
    {
        await SeedAsync(
        [
            new Widget { Id = Guid.NewGuid(), Name = "Alice", Score = 1 },
            new Widget { Id = Guid.NewGuid(), Name = "Bob",   Score = 2 },
            new Widget { Id = Guid.NewGuid(), Name = "Alice2", Score = 3 },
        ]);

        var page = await _ctx.Widgets.ToKeysetPageAsync(
            DefaultOpts(search: "alice"),
            WidgetSortMap.Value, WidgetSortMap.SearchColumns, WidgetSortMap.Projection);

        page.Items.Count.ShouldBe(2);
        page.Total.ShouldBe(2);
        page.Items.ShouldAllBe(w => w.Name.Contains("Alice", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SearchFilter_CaseInsensitive()
    {
        await SeedAsync([new Widget { Id = Guid.NewGuid(), Name = "UPPER", Score = 1 }]);

        var page = await _ctx.Widgets.ToKeysetPageAsync(
            DefaultOpts(search: "upper"),
            WidgetSortMap.Value, WidgetSortMap.SearchColumns, WidgetSortMap.Projection);

        page.Items.Count.ShouldBe(1);
    }

    // ── Sort asc / desc on string column ─────────────────────────────────────

    [Fact]
    public async Task SortAsc_ByName()
    {
        await SeedAsync(
        [
            new Widget { Id = Guid.NewGuid(), Name = "Zebra", Score = 1 },
            new Widget { Id = Guid.NewGuid(), Name = "Apple", Score = 2 },
            new Widget { Id = Guid.NewGuid(), Name = "Mango", Score = 3 },
        ]);

        var page = await _ctx.Widgets.ToKeysetPageAsync(
            DefaultOpts(sortBy: "name", dir: PaginationSortDir.Asc),
            WidgetSortMap.Value, WidgetSortMap.SearchColumns, WidgetSortMap.Projection);

        page.Items.Select(w => w.Name).ShouldBe(["Apple", "Mango", "Zebra"]);
    }

    [Fact]
    public async Task SortDesc_ByName()
    {
        await SeedAsync(
        [
            new Widget { Id = Guid.NewGuid(), Name = "Zebra", Score = 1 },
            new Widget { Id = Guid.NewGuid(), Name = "Apple", Score = 2 },
            new Widget { Id = Guid.NewGuid(), Name = "Mango", Score = 3 },
        ]);

        var page = await _ctx.Widgets.ToKeysetPageAsync(
            DefaultOpts(sortBy: "name", dir: PaginationSortDir.Desc),
            WidgetSortMap.Value, WidgetSortMap.SearchColumns, WidgetSortMap.Projection);

        page.Items.Select(w => w.Name).ShouldBe(["Zebra", "Mango", "Apple"]);
    }

    // ── Sort asc / desc on int column ─────────────────────────────────────────

    [Fact]
    public async Task SortAsc_ByScore()
    {
        await SeedAsync(
        [
            new Widget { Id = Guid.NewGuid(), Name = "C", Score = 30 },
            new Widget { Id = Guid.NewGuid(), Name = "A", Score = 10 },
            new Widget { Id = Guid.NewGuid(), Name = "B", Score = 20 },
        ]);

        var page = await _ctx.Widgets.ToKeysetPageAsync(
            DefaultOpts(sortBy: "score", dir: PaginationSortDir.Asc),
            WidgetSortMap.Value, WidgetSortMap.SearchColumns, WidgetSortMap.Projection);

        page.Items.Select(w => w.Score).ShouldBe([10, 20, 30]);
    }

    [Fact]
    public async Task SortDesc_ByScore()
    {
        await SeedAsync(
        [
            new Widget { Id = Guid.NewGuid(), Name = "C", Score = 30 },
            new Widget { Id = Guid.NewGuid(), Name = "A", Score = 10 },
            new Widget { Id = Guid.NewGuid(), Name = "B", Score = 20 },
        ]);

        var page = await _ctx.Widgets.ToKeysetPageAsync(
            DefaultOpts(sortBy: "score", dir: PaginationSortDir.Desc),
            WidgetSortMap.Value, WidgetSortMap.SearchColumns, WidgetSortMap.Projection);

        page.Items.Select(w => w.Score).ShouldBe([30, 20, 10]);
    }

    // ── Tiebreaker on equal sort values ──────────────────────────────────────

    [Fact]
    public async Task Tiebreaker_EqualSortValues_OrderedById()
    {
        var id1 = new Guid("00000000-0000-0000-0000-000000000001");
        var id2 = new Guid("00000000-0000-0000-0000-000000000002");
        var id3 = new Guid("00000000-0000-0000-0000-000000000003");

        await SeedAsync(
        [
            new Widget { Id = id3, Name = "Same", Score = 5 },
            new Widget { Id = id1, Name = "Same", Score = 5 },
            new Widget { Id = id2, Name = "Same", Score = 5 },
        ]);

        var page = await _ctx.Widgets.ToKeysetPageAsync(
            DefaultOpts(sortBy: "name", dir: PaginationSortDir.Asc),
            WidgetSortMap.Value, WidgetSortMap.SearchColumns, WidgetSortMap.Projection);

        page.Items.Select(w => w.Id).ShouldBe([id1, id2, id3]);
    }

    [Fact]
    public async Task Tiebreaker_CursorContinuation_TiebreakerId()
    {
        var id1 = new Guid("00000000-0000-0000-0000-000000000001");
        var id2 = new Guid("00000000-0000-0000-0000-000000000002");
        var id3 = new Guid("00000000-0000-0000-0000-000000000003");

        await SeedAsync(
        [
            new Widget { Id = id1, Name = "Same", Score = 5 },
            new Widget { Id = id2, Name = "Same", Score = 5 },
            new Widget { Id = id3, Name = "Same", Score = 5 },
        ]);

        // Page 1: get first 2.
        var page1 = await _ctx.Widgets.ToKeysetPageAsync(
            DefaultOpts(pageSize: 2, sortBy: "name"),
            WidgetSortMap.Value, WidgetSortMap.SearchColumns, WidgetSortMap.Projection);

        page1.Items.Select(w => w.Id).ShouldBe([id1, id2]);
        page1.NextCursor.ShouldNotBeNull();

        // Page 2: should return only id3.
        var page2 = await _ctx.Widgets.ToKeysetPageAsync(
            DefaultOpts(pageSize: 2, cursor: page1.NextCursor, sortBy: "name"),
            WidgetSortMap.Value, WidgetSortMap.SearchColumns, WidgetSortMap.Projection);

        page2.Items.Select(w => w.Id).ShouldBe([id3]);
        page2.NextCursor.ShouldBeNull();
    }

    // ── Total count independent of cursor ────────────────────────────────────

    [Fact]
    public async Task Total_IsIndependentOfCursor()
    {
        var widgets = Enumerable.Range(1, 10)
            .Select(i => new Widget { Id = Guid.NewGuid(), Name = $"Item{i:D2}", Score = i })
            .ToList();
        await SeedAsync(widgets);

        // Get page 1 to obtain a cursor.
        var page1 = await _ctx.Widgets.ToKeysetPageAsync(
            DefaultOpts(pageSize: 3), WidgetSortMap.Value, WidgetSortMap.SearchColumns, WidgetSortMap.Projection);

        page1.Total.ShouldBe(10);

        // Page 2 via cursor — total should still be 10.
        var page2 = await _ctx.Widgets.ToKeysetPageAsync(
            DefaultOpts(pageSize: 3, cursor: page1.NextCursor),
            WidgetSortMap.Value, WidgetSortMap.SearchColumns, WidgetSortMap.Projection);

        page2.Total.ShouldBe(10);
    }

    // ── Projection is applied ─────────────────────────────────────────────────

    [Fact]
    public async Task Projection_IsApplied()
    {
        var id = Guid.NewGuid();
        await SeedAsync([new Widget { Id = id, Name = "Projected", Score = 99 }]);

        var page = await _ctx.Widgets.ToKeysetPageAsync(
            DefaultOpts(), WidgetSortMap.Value, WidgetSortMap.SearchColumns, WidgetSortMap.Projection);

        var item = page.Items.ShouldHaveSingleItem();
        item.ShouldBeOfType<WidgetDto>();
        item.Id.ShouldBe(id);
        item.Name.ShouldBe("Projected");
        item.Score.ShouldBe(99);
    }

    // ── Default sort (no SortBy) ──────────────────────────────────────────────

    [Fact]
    public async Task DefaultSort_UsedWhenSortByIsNull()
    {
        await SeedAsync(
        [
            new Widget { Id = Guid.NewGuid(), Name = "Zebra", Score = 1 },
            new Widget { Id = Guid.NewGuid(), Name = "Apple", Score = 2 },
        ]);

        // Default sort is "name" asc.
        var page = await _ctx.Widgets.ToKeysetPageAsync(
            DefaultOpts(sortBy: null), WidgetSortMap.Value, WidgetSortMap.SearchColumns, WidgetSortMap.Projection);

        page.Items[0].Name.ShouldBe("Apple");
        page.Items[1].Name.ShouldBe("Zebra");
    }
}
