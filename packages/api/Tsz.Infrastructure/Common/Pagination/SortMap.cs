namespace Tsz.Infrastructure.Common.Pagination;

public sealed record SortColumn<TEntity>(
    string Key,
    LambdaExpression Selector,
    Type ClrType);

public sealed class SortMap<TEntity>
{
    private readonly Dictionary<string, SortColumn<TEntity>> _map;

    public SortMap(SortColumn<TEntity> defaultColumn, params SortColumn<TEntity>[] more)
    {
        Default = defaultColumn;
        _map = new Dictionary<string, SortColumn<TEntity>>(StringComparer.OrdinalIgnoreCase)
        {
            [defaultColumn.Key] = defaultColumn,
        };
        foreach (var col in more)
            _map[col.Key] = col;
    }

    public SortColumn<TEntity> Default { get; }

    public bool TryGet(string? key, out SortColumn<TEntity> column)
    {
        if (key is null)
        {
            column = Default;
            return true;
        }
        return _map.TryGetValue(key, out column!);
    }

    public IReadOnlyCollection<string> AllowedKeys => _map.Keys;
}
