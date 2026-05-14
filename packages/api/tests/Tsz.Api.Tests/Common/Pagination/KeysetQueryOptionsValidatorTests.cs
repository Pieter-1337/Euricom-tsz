using System.Linq.Expressions;
using System.Text.Json;
using Shouldly;
using Tsz.Infrastructure.Common.Pagination;
using PaginationSortDir = Tsz.Infrastructure.Common.Pagination.SortDirection;

namespace Tsz.Api.Tests.Common.Pagination;

// ── Concrete validator for tests ──────────────────────────────────────────────

internal sealed class TestPaginationEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Score { get; set; }
}

internal sealed class TestKeysetQueryValidator
    : KeysetQueryOptionsValidator<KeysetQueryOptions, TestPaginationEntity>
{
    private static readonly SortMap<TestPaginationEntity> SortMap = new(
        new SortColumn<TestPaginationEntity>(
            "name",
            (Expression<Func<TestPaginationEntity, string>>)(e => e.Name),
            typeof(string)),
        new SortColumn<TestPaginationEntity>(
            "score",
            (Expression<Func<TestPaginationEntity, int>>)(e => e.Score),
            typeof(int)));

    public TestKeysetQueryValidator() : base(SortMap) { }
}

// ── Tests ─────────────────────────────────────────────────────────────────────

public class KeysetQueryOptionsValidatorTests
{
    private static readonly TestKeysetQueryValidator Validator = new();

    private static KeysetQueryOptions Valid() =>
        new(Search: null, SortBy: null, SortDir: PaginationSortDir.Asc,
            PageSize: 20, Cursor: null, DeletedOnly: false);

    // ── Search ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Search_Null_Passes()
    {
        var result = await Validator.ValidateAsync(Valid() with { Search = null });
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Search_200Chars_Passes()
    {
        var result = await Validator.ValidateAsync(Valid() with { Search = new string('a', 200) });
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Search_201Chars_Fails()
    {
        var result = await Validator.ValidateAsync(Valid() with { Search = new string('a', 201) });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(KeysetQueryOptions.Search));
    }

    // ── SortBy ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SortBy_Null_Passes()
    {
        var result = await Validator.ValidateAsync(Valid() with { SortBy = null });
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task SortBy_AllowedKey_Passes()
    {
        var result = await Validator.ValidateAsync(Valid() with { SortBy = "name" });
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task SortBy_AllowedKey_CaseInsensitive_Passes()
    {
        var result = await Validator.ValidateAsync(Valid() with { SortBy = "NAME" });
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task SortBy_UnknownKey_Fails()
    {
        var result = await Validator.ValidateAsync(Valid() with { SortBy = "unknown" });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(KeysetQueryOptions.SortBy));
    }

    // ── PageSize ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task PageSize_1_Passes()
    {
        var result = await Validator.ValidateAsync(Valid() with { PageSize = 1 });
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task PageSize_200_Passes()
    {
        var result = await Validator.ValidateAsync(Valid() with { PageSize = 200 });
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task PageSize_Zero_Fails()
    {
        var result = await Validator.ValidateAsync(Valid() with { PageSize = 0 });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(KeysetQueryOptions.PageSize));
    }

    [Fact]
    public async Task PageSize_201_Fails()
    {
        var result = await Validator.ValidateAsync(Valid() with { PageSize = 201 });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(KeysetQueryOptions.PageSize));
    }

    // ── Cursor ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Cursor_Null_Passes()
    {
        var result = await Validator.ValidateAsync(Valid() with { Cursor = null });
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Cursor_ValidCursor_Passes()
    {
        var sortJson = JsonDocument.Parse("\"alice\"").RootElement.Clone();
        var validCursor = new KeysetCursor(1, sortJson, Guid.NewGuid()).Encode();

        var result = await Validator.ValidateAsync(Valid() with { Cursor = validCursor });
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Cursor_GarbageValue_Fails()
    {
        var result = await Validator.ValidateAsync(Valid() with { Cursor = "garbage!!!" });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(KeysetQueryOptions.Cursor));
    }

    [Fact]
    public async Task Cursor_UnknownVersion_Fails()
    {
        var sortJson = JsonDocument.Parse("\"x\"").RootElement.Clone();
        var oldVersionCursor = new KeysetCursor(99, sortJson, Guid.NewGuid()).Encode();

        var result = await Validator.ValidateAsync(Valid() with { Cursor = oldVersionCursor });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(KeysetQueryOptions.Cursor));
    }

    // ── DeletedOnly ───────────────────────────────────────────────────────────

    [Fact]
    public async Task DeletedOnly_True_Passes()
    {
        var result = await Validator.ValidateAsync(Valid() with { DeletedOnly = true });
        result.IsValid.ShouldBeTrue();
    }
}
