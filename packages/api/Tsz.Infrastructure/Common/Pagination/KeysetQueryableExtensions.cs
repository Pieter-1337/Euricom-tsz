using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Tsz.Infrastructure.Common.Pagination;

public static class KeysetQueryableExtensions
{
    public static async Task<KeysetPage<TDto>> ToKeysetPageAsync<TEntity, TDto>(
        this IQueryable<TEntity> baseQuery,
        KeysetQueryOptions opts,
        SortMap<TEntity> sortMap,
        Expression<Func<TEntity, string>>[] searchableColumns,
        Expression<Func<TEntity, TDto>> projection,
        CancellationToken ct = default)
    {
        // Step 1: Resolve sort column.
        if (!sortMap.TryGet(opts.SortBy, out var sortCol))
            throw new ArgumentException($"Unknown sort key '{opts.SortBy}'.", nameof(opts));

        // Step 2: Apply search predicate.
        var filteredQuery = ApplySearch(baseQuery, opts.Search, searchableColumns);

        // Step 7: Kick off count in parallel — filtered query, no cursor / order / take.
        var countTask = filteredQuery.CountAsync(ct);

        // Step 3: Apply cursor predicate.
        var pagedQuery = filteredQuery;
        if (KeysetCursor.TryDecode(opts.Cursor, out var cursor) && cursor is not null)
            pagedQuery = ApplyCursorPredicate(pagedQuery, cursor, sortCol, opts.SortDir);

        // Step 4: Apply order.
        pagedQuery = ApplyOrder(pagedQuery, sortCol, opts.SortDir);

        var pageSize = Math.Min(Math.Max(opts.PageSize, 1), 200);

        // Step 5: Materialise pageSize + 1 projected items.
        var rawItems = await pagedQuery
            .Take(pageSize + 1)
            .Select(projection)
            .ToListAsync(ct);

        // Step 6: Build next cursor. If we got pageSize+1 items there is another page.
        string? nextCursor = null;
        if (rawItems.Count > pageSize)
        {
            rawItems.RemoveAt(rawItems.Count - 1);

            // Get the last entity on the page to read sort value + Id.
            var lastEntity = await pagedQuery
                .Take(pageSize)
                .Select(BuildEntityCursorSelector<TEntity>(sortCol))
                .LastOrDefaultAsync(ct);

            if (lastEntity is not null)
            {
                var sortJson = JsonSerializer.SerializeToDocument(lastEntity.SortValue).RootElement.Clone();
                nextCursor = new KeysetCursor(1, sortJson, lastEntity.Id).Encode();
            }
        }

        // Step 7: Await the count.
        var total = await countTask;

        return new KeysetPage<TDto>(rawItems.AsReadOnly(), nextCursor, total);
    }

    // ── Search ────────────────────────────────────────────────────────────────

    private static IQueryable<TEntity> ApplySearch<TEntity>(
        IQueryable<TEntity> query,
        string? search,
        Expression<Func<TEntity, string>>[] columns)
    {
        if (string.IsNullOrWhiteSpace(search) || columns.Length == 0)
            return query;

        var term = search.ToLower();
        var param = Expression.Parameter(typeof(TEntity), "e");
        Expression? predicate = null;

        var toLowerMethod = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;
        var containsMethod = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;

        foreach (var col in columns)
        {
            var rebound = RebindParameter(col.Body, col.Parameters[0], param);
            var lower = Expression.Call(rebound, toLowerMethod);
            var contains = Expression.Call(lower, containsMethod, Expression.Constant(term));

            predicate = predicate is null ? contains : Expression.OrElse(predicate, contains);
        }

        if (predicate is null)
            return query;

        return query.Where(Expression.Lambda<Func<TEntity, bool>>(predicate, param));
    }

    // ── Cursor predicate ──────────────────────────────────────────────────────

    private static IQueryable<TEntity> ApplyCursorPredicate<TEntity>(
        IQueryable<TEntity> query,
        KeysetCursor cursor,
        SortColumn<TEntity> sortCol,
        SortDirection dir)
    {
        var param = Expression.Parameter(typeof(TEntity), "e");
        var sortBody = RebindParameter(sortCol.Selector.Body, sortCol.Selector.Parameters[0], param);
        var idProp = Expression.Property(param, "Id");

        var sortValue = DeserializeSortValue(cursor.SortValue, sortCol.ClrType);
        var sortConst = Expression.Constant(sortValue, sortCol.ClrType);
        var idConst = Expression.Constant(cursor.Id, typeof(Guid));

        // Asc:  sortCol > sortValue  ||  (sortCol == sortValue && Id > cursorId)
        // Desc: sortCol < sortValue  ||  (sortCol == sortValue && Id < cursorId)
        //
        // For non-primitive types (e.g. string), Expression.GreaterThan is not defined.
        // Use IComparable<T>.CompareTo(other) > 0 instead, which EF Core translates and
        // plain LINQ evaluates correctly.
        var compareToMethod = sortCol.ClrType
            .GetMethod(nameof(IComparable.CompareTo), [sortCol.ClrType])
            ?? throw new InvalidOperationException(
                $"Sort column CLR type '{sortCol.ClrType.Name}' does not implement IComparable<T>.CompareTo.");

        var compareResult = Expression.Call(sortBody, compareToMethod, sortConst);
        var zero = Expression.Constant(0, typeof(int));

        Expression main = dir == SortDirection.Asc
            ? Expression.GreaterThan(compareResult, zero)
            : Expression.LessThan(compareResult, zero);

        // Guid always supports >, < via Expression.GreaterThan.
        Expression idMain = dir == SortDirection.Asc
            ? Expression.GreaterThan(idProp, idConst)
            : Expression.LessThan(idProp, idConst);

        Expression tie = Expression.AndAlso(
            Expression.Equal(Expression.Call(sortBody, compareToMethod, sortConst), zero),
            idMain);

        var predicate = Expression.Lambda<Func<TEntity, bool>>(
            Expression.OrElse(main, tie), param);

        return query.Where(predicate);
    }

    // ── Ordering ──────────────────────────────────────────────────────────────

    private static IQueryable<TEntity> ApplyOrder<TEntity>(
        IQueryable<TEntity> query,
        SortColumn<TEntity> sortCol,
        SortDirection dir)
    {
        var param = Expression.Parameter(typeof(TEntity), "e");
        var sortBody = RebindParameter(sortCol.Selector.Body, sortCol.Selector.Parameters[0], param);
        var idProp = Expression.Property(param, "Id");
        var sortLambda = Expression.Lambda(sortBody, param);
        var idLambda = Expression.Lambda<Func<TEntity, Guid>>(idProp, param);

        var orderName = dir == SortDirection.Asc ? "OrderBy" : "OrderByDescending";
        var thenName = dir == SortDirection.Asc ? "ThenBy" : "ThenByDescending";

        var ordered = (IOrderedQueryable<TEntity>)CallQueryableMethod(query, orderName, sortLambda);

        return CallQueryableMethod(ordered, thenName, idLambda);
    }

    private static IQueryable<TEntity> CallQueryableMethod<TEntity>(
        IQueryable<TEntity> query,
        string methodName,
        LambdaExpression keySelector)
    {
        var method = typeof(Queryable)
            .GetMethods()
            .First(m => m.Name == methodName && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(TEntity), keySelector.ReturnType);

        return (IQueryable<TEntity>)method.Invoke(null, [query, keySelector])!;
    }

    // ── Cursor data selector ──────────────────────────────────────────────────

    /// <summary>
    /// Projects each entity to a (SortValue as object, Id) pair so we can build
    /// the next-page cursor without a separate typed intermediate DTO.
    /// This works on plain LINQ (unit tests) and EF Core (production).
    /// </summary>
    private static Expression<Func<TEntity, EntityCursorData>> BuildEntityCursorSelector<TEntity>(
        SortColumn<TEntity> sortCol)
    {
        var param = Expression.Parameter(typeof(TEntity), "e");
        var sortBody = RebindParameter(sortCol.Selector.Body, sortCol.Selector.Parameters[0], param);
        var idProp = Expression.Property(param, "Id");

        // Box the sort value to object so one projection type covers all column types.
        var sortAsObject = Expression.Convert(sortBody, typeof(object));

        var ctor = typeof(EntityCursorData).GetConstructor([typeof(object), typeof(Guid)])!;
        var newExpr = Expression.New(ctor, sortAsObject, idProp);

        return Expression.Lambda<Func<TEntity, EntityCursorData>>(newExpr, param);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Expression RebindParameter(
        Expression body,
        ParameterExpression oldParam,
        ParameterExpression newParam)
        => new ParameterRebinder(oldParam, newParam).Visit(body);

    private sealed class ParameterRebinder(ParameterExpression oldParam, ParameterExpression newParam)
        : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
            => node == oldParam ? newParam : base.VisitParameter(node);
    }

    private static object? DeserializeSortValue(JsonElement element, Type clrType)
    {
        if (clrType == typeof(string)) return element.GetString();
        if (clrType == typeof(int)) return element.GetInt32();
        if (clrType == typeof(long)) return element.GetInt64();
        if (clrType == typeof(DateTime)) return element.GetDateTime();
        if (clrType == typeof(DateTimeOffset)) return element.GetDateTimeOffset();
        if (clrType == typeof(Guid)) return element.GetGuid();
        if (clrType == typeof(double)) return element.GetDouble();
        if (clrType == typeof(decimal)) return element.GetDecimal();
        return JsonSerializer.Deserialize(element.GetRawText(), clrType);
    }

    private sealed class EntityCursorData(object? sortValue, Guid id)
    {
        public object? SortValue { get; } = sortValue;
        public Guid Id { get; } = id;
    }
}
