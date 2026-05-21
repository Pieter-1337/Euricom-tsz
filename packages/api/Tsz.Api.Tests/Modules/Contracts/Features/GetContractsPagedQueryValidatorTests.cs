using Shouldly;
using Tsz.Modules.Contracts.Features;
using SortDir = Tsz.Infrastructure.Common.Pagination.SortDirection;

namespace Tsz.Api.Tests.Modules.Contracts.Features;

public class GetContractsPagedQueryValidatorTests
{
    private static readonly GetContractsPagedQueryValidator Validator = new();

    private static GetContractsPagedQuery Query(
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
    public async Task Valid_DefaultQuery_Passes()
    {
        var result = await Validator.ValidateAsync(Query());
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task SortBy_AllowedKeys_Pass()
    {
        foreach (var key in new[] { "number", "subject", "start" })
        {
            var result = await Validator.ValidateAsync(Query(sortBy: key));
            result.IsValid.ShouldBeTrue($"'{key}' should be an allowed sort key");
        }
    }

    [Fact]
    public async Task SortBy_UnknownKey_Fails()
    {
        var result = await Validator.ValidateAsync(Query(sortBy: "unknown"));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(GetContractsPagedQuery.SortBy));
    }

    [Fact]
    public async Task PageSize_TooBig_Fails()
    {
        var result = await Validator.ValidateAsync(Query(pageSize: 201));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(GetContractsPagedQuery.PageSize));
    }

    [Fact]
    public async Task Cursor_InvalidBase64_Fails()
    {
        var result = await Validator.ValidateAsync(Query(cursor: "garbage!!!"));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(GetContractsPagedQuery.Cursor));
    }
}
