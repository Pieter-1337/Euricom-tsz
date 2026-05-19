using Shouldly;
using Tsz.Api.Modules.Users;
using Tsz.Api.Modules.Users.Features;
using Tsz.Infrastructure.Common.Pagination;
using SortDir = Tsz.Infrastructure.Common.Pagination.SortDirection;

namespace Tsz.Api.Tests.Modules.Users.Features;

public class GetUsersPagedQueryValidatorTests
{
    private static readonly GetUsersPagedQueryValidator Validator = new();

    private static GetUsersPagedQuery Query(
        string? search = null,
        string? sortBy = null,
        SortDir sortDir = SortDir.Asc,
        int pageSize = 50,
        string? cursor = null,
        bool includeDeleted = false)
        => new(search, sortBy, sortDir, pageSize, cursor, includeDeleted);

    [Fact]
    public async Task Valid_DefaultQuery_Passes()
    {
        var result = await Validator.ValidateAsync(Query());
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Search_ExceedsMaxLength_Fails()
    {
        var longSearch = new string('x', 201);
        var result = await Validator.ValidateAsync(Query(search: longSearch));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(GetUsersPagedQuery.Search));
    }

    [Fact]
    public async Task SortBy_AllowedKey_Passes()
    {
        foreach (var key in new[] { "name", "email" })
        {
            var result = await Validator.ValidateAsync(Query(sortBy: key));
            result.IsValid.ShouldBeTrue($"'{key}' should be an allowed sort key");
        }
    }

    [Fact]
    public async Task SortBy_UnknownKey_Fails()
    {
        var result = await Validator.ValidateAsync(Query(sortBy: "ssn"));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(GetUsersPagedQuery.SortBy));
    }

    [Fact]
    public async Task PageSize_TooBig_Fails()
    {
        // PageSize <= 0 is normalized to 50 by KeysetQueryOptions, so the validator
        // only catches values above the maximum (200).
        var tooBig = await Validator.ValidateAsync(Query(pageSize: 201));
        tooBig.IsValid.ShouldBeFalse();
        tooBig.Errors.ShouldContain(e => e.PropertyName == nameof(GetUsersPagedQuery.PageSize));
    }

    [Fact]
    public async Task Cursor_InvalidBase64_Fails()
    {
        var result = await Validator.ValidateAsync(Query(cursor: "garbage!!!"));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(GetUsersPagedQuery.Cursor));
    }
}
