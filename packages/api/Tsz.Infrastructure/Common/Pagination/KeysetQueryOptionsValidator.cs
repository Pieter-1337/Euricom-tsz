using FluentValidation;

namespace Tsz.Infrastructure.Common.Pagination;

/// <summary>
/// Base validator for any query that carries <see cref="KeysetQueryOptions"/>.
/// Subclass and call the base constructor with the relevant <see cref="SortMap{TEntity}"/>
/// to pick up the universal pagination rules, then add entity-specific rules on top.
/// </summary>
public abstract class KeysetQueryOptionsValidator<TQuery, TEntity> : AbstractValidator<TQuery>
    where TQuery : KeysetQueryOptions
{
    protected KeysetQueryOptionsValidator(SortMap<TEntity> sortMap)
    {
        RuleFor(q => q.Search)
            .MaximumLength(200)
            .When(q => q.Search is not null)
            .WithMessage("Search term must not exceed 200 characters.");

        RuleFor(q => q.SortBy)
            .Must(key => sortMap.TryGet(key, out _))
            .When(q => q.SortBy is not null)
            .WithMessage(q => $"'{q.SortBy}' is not a sortable column. Allowed: {string.Join(", ", sortMap.AllowedKeys)}.");

        RuleFor(q => q.PageSize)
            .InclusiveBetween(1, 200)
            .WithMessage("PageSize must be between 1 and 200.");

        RuleFor(q => q.Cursor)
            .Must(c => KeysetCursor.TryDecode(c, out _))
            .When(q => q.Cursor is not null)
            .WithMessage("Cursor is invalid or has expired.");
    }
}
