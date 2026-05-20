namespace Tsz.Infrastructure.Common.Pagination;

/// <summary>
/// One dimension of a paged-search OR. Either a single string column (lowered-Contains
/// against the search term) or a collection of enum values (each enum's name checked
/// for the term; matching enum values are pre-computed and the predicate becomes
/// <c>collection.Any(e =&gt; matched.Contains(e))</c>).
/// </summary>
public abstract record SearchableField<TEntity>
{
    public static SearchableField<TEntity> Column(Expression<Func<TEntity, string>> selector) =>
        new StringColumn(selector);

    public static SearchableField<TEntity> Column<TEnum>(Expression<Func<TEntity, IEnumerable<TEnum>>> selector)
        where TEnum : struct, Enum =>
        new EnumCollectionColumn<TEnum>(selector);

    /// <summary>Build a predicate for this field given the (lowercased) search term. Returns null if no rows can match.</summary>
    internal abstract Expression<Func<TEntity, bool>>? Build(string term);

    internal sealed record StringColumn(Expression<Func<TEntity, string>> Selector) : SearchableField<TEntity>
    {
        internal override Expression<Func<TEntity, bool>> Build(string term)
        {
            var param = Expression.Parameter(typeof(TEntity), "e");
            var rebound = Selector.Body.Replace(Selector.Parameters[0], param);
            var toLower = Expression.Call(rebound, typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!);
            var contains = Expression.Call(
                toLower,
                typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!,
                Expression.Constant(term));
            return Expression.Lambda<Func<TEntity, bool>>(contains, param);
        }
    }

    internal sealed record EnumCollectionColumn<TEnum>(Expression<Func<TEntity, IEnumerable<TEnum>>> Selector)
        : SearchableField<TEntity>
        where TEnum : struct, Enum
    {
        internal override Expression<Func<TEntity, bool>>? Build(string term)
        {
            var matched = Enum.GetValues<TEnum>()
                .Where(v => v.ToString().Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (matched.Length == 0) return null;

            var param = Expression.Parameter(typeof(TEntity), "e");
            var collectionExpr = Selector.Body.Replace(Selector.Parameters[0], param);

            // collectionExpr.Any(v => matched.Contains(v))
            var vParam = Expression.Parameter(typeof(TEnum), "v");
            var containsMethod = typeof(Enumerable).GetMethods()
                .First(m => m.Name == nameof(Enumerable.Contains) && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(TEnum));
            var containsCall = Expression.Call(null, containsMethod, Expression.Constant(matched), vParam);
            var inner = Expression.Lambda<Func<TEnum, bool>>(containsCall, vParam);

            var anyMethod = typeof(Enumerable).GetMethods()
                .First(m => m.Name == nameof(Enumerable.Any) && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(TEnum));
            var anyCall = Expression.Call(null, anyMethod, collectionExpr, inner);

            return Expression.Lambda<Func<TEntity, bool>>(anyCall, param);
        }
    }
}
