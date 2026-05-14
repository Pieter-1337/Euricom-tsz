# Plan: List scaffolding (`listBase`) for packages/api + packages/web

## Goal

Extract the recurring "paged, sorted, searchable list" plumbing into reusable
primitives on both sides of the wire so each new list page is mostly column
definitions + a data-source hook. The equivalent of `formBase` (see
[../form/plan.md](../form/plan.md)) but for read-side lists.

Concretely, every consumer should get:

- Free-text search (case-insensitive `LIKE` over a caller-defined set of columns).
- Single-column sort with `asc/desc` toggle.
- Keyset (a.k.a. seek) pagination — opaque base64 cursor, no `OFFSET`.
- Total count of rows matching the filter (cursor-independent).
- Optional toggle to include soft-deleted rows (bypasses EF query filter).
- Infinite scroll on the client (TanStack Query `useInfiniteQuery` + IntersectionObserver).
- Debounced search input.

Each route owns only: column definitions, the entity's sortable column map,
which columns get searched, and the row click target.

## Context

- One real list today: `/admin/users` — dumb `GET /api/users` returning
  `List<UserDto>`, dumb `<Table>` with no sort/search/pagination. The user-list
  plan ([../../users/plan-list.md](../../users/plan-list.md)) describes the
  user-specific behaviour and screenshot target; this plan is the generic
  infrastructure that user list will be the first consumer of.
- Backend has no pagination primitives yet. CQRS bus is in place; this plumbing
  hangs off `IQueryHandler` / `IRequestHandler`.
- Frontend has TanStack Query (no `useInfiniteQuery` usage yet), TanStack Router,
  shadcn `Table` + `Input` + `Button` already installed. No `<Switch>` yet —
  add via `bun x shadcn@latest add switch` when the active-toggle ships.

## Non-goals

- Server-driven **multi-column** sort. Single column + stable `Id` tiebreaker
  covers every list we have or plan for. Easy to extend later.
- Full-text search (SQLite FTS5, trigram). Default is `LOWER(col) LIKE '%term%'`.
- URL-persistence of `search`/`sort`/`scrollPos`. Out of scope for v1; one-day
  add via TanStack Router `useSearch` once we agree on the URL contract.
- Faceted filters (multi-select chips, date ranges, etc.). When the first list
  actually needs one, add `IListFilter<TEntity>` as a separate plan.
- Saved views, bulk actions, row selection. Future plans.
- An off-the-shelf data-grid (AG Grid, TanStack Table). The scaffolding is
  deliberately thin so callers stay in plain JSX.

---

## Primitives — backend

Live under `Tsz.Infrastructure` so any module can take a dependency without
reaching into a sibling module's namespace. New folder: `Common/Pagination/`.

### Files

```
packages/api/Tsz.Infrastructure/Common/Pagination/
  KeysetCursor.cs          — opaque cursor record + Encode / TryDecode
  KeysetPage.cs            — generic response { Items, NextCursor, Total }
  KeysetQueryOptions.cs    — request record (Search, SortBy, SortDir, PageSize,
                             Cursor, IncludeDeleted)
  SortMap.cs               — IReadOnlyDictionary<string, SortColumn<TEntity>>
                             helper + the SortColumn<TEntity> record
  KeysetQueryableExtensions.cs
                           — IQueryable<TEntity>.ApplyKeysetPaging(...)
                             single entry point; returns Task<KeysetPage<TDto>>
```

### `KeysetCursor`

```csharp
public sealed record KeysetCursor(int V, JsonElement SortValue, Guid Id)
{
    public string Encode() { /* base64url(JSON) */ }
    public static bool TryDecode(string? raw, out KeysetCursor? cursor) { /* ... */ }
}
```

- `V`: schema version. Bump on breaking change to the cursor shape; older
  cursors → `TryDecode` returns false → caller returns `400`.
- `SortValue`: the value of the sort column on the **last row** of the previous
  page. Stored as `JsonElement` so we can round-trip `string`/`int`/`DateTime`/
  enum-as-string without compile-time knowledge of the column type. The helper
  (below) is responsible for casting `JsonElement` back to the column's CLR type
  per the sort map's `ClrType`.
- `Id`: same row's `Id`, used as the strict tiebreaker so ties on the sort
  column are deterministic across pages.
- Encoded with `JsonSerializer` → UTF-8 → `Base64UrlEncoder.Encode`. Not signed
  — the cursor carries no server-side secret; admin-only endpoints, no security
  gain from signing.

### `KeysetPage<T>`

```csharp
public sealed record KeysetPage<T>(IReadOnlyList<T> Items, string? NextCursor, int Total);
```

- `NextCursor` is `null` when there's no further page (we fetched `pageSize + 1`
  and got back ≤ `pageSize`).
- `Total` is a separate `COUNT(*)` over the **same WHERE** (search + soft-delete
  filter) without the cursor predicate. Negligible for thousands of rows; pay
  the cost.

### `KeysetQueryOptions`

```csharp
public sealed record KeysetQueryOptions(
    string? Search,
    string? SortBy,
    SortDirection SortDir,
    int PageSize,
    string? Cursor,
    bool IncludeDeleted);

public enum SortDirection { Asc, Desc }
```

- Default `PageSize`: 50. Cap: 200 (enforced by validator).
- `SortBy` null → caller's `SortMap.Default` applies.
- All caller-bound via `[AsParameters]` so URL stays flat.

### `SortMap<TEntity>` + `SortColumn<TEntity>`

```csharp
public sealed record SortColumn<TEntity>(
    string Key,                              // wire name, e.g. "name"
    LambdaExpression Selector,               // Expression<Func<TEntity, TValue>>
    Type ClrType);                           // for JsonElement → TValue casting

public sealed class SortMap<TEntity>
{
    public SortMap(SortColumn<TEntity> defaultColumn, params SortColumn<TEntity>[] more);
    public SortColumn<TEntity> Default { get; }
    public bool TryGet(string key, out SortColumn<TEntity> column);   // case-insensitive
    public IReadOnlyCollection<string> AllowedKeys { get; }           // for validator
}
```

- Caller defines once per entity, e.g. `static readonly SortMap<User> Sort = new(...)`.
- `Selector` is `LambdaExpression` (untyped) because each column may have a
  different value type; the helper bakes it into a typed `OrderBy` call at
  runtime via reflection on `Queryable.OrderBy`. This is one-time work per
  request, not per row.

### `KeysetQueryableExtensions`

Single entry point:

```csharp
public static Task<KeysetPage<TDto>> ToKeysetPageAsync<TEntity, TDto>(
    this IQueryable<TEntity> baseQuery,                                  // already filtered
    KeysetQueryOptions opts,
    SortMap<TEntity> sortMap,
    Expression<Func<TEntity, string>>[] searchableColumns,                // for LIKE
    Expression<Func<TEntity, TDto>> projection,
    CancellationToken ct);
```

Internally:

1. **Resolve sort column** — `sortMap.TryGet(opts.SortBy)` or `Default`. Unknown
   key bubbles up as exception → caller's validator should have caught it; if
   it slips through, `400` via `ValidationException`.
2. **Apply search predicate** — if `opts.Search` non-empty, build
   `searchableColumns.Aggregate((a, b) => EF.Functions.Like(LOWER(a), term) || EF.Functions.Like(LOWER(b), term))`
   on top of `baseQuery`. EF parameterises so no injection; `LOWER` + literal
   `%term%` works on every provider we'd target.
3. **Apply cursor predicate** — if `opts.Cursor` decodes, append
   `WHERE sortCol [>|<] @sortValue OR (sortCol = @sortValue AND Id [>|<] @id)`,
   direction depending on `opts.SortDir`. The `JsonElement` → CLR cast uses
   `sortColumn.ClrType`.
4. **Apply order** — `ORDER BY sortCol [Asc|Desc], Id [Asc|Desc]`.
5. **Materialise** — `Take(opts.PageSize + 1).Select(projection).ToListAsync()`.
   The `+ 1` lets us decide whether to emit `NextCursor`.
6. **Build cursor** — from the last in-page row (the `pageSize`-th), reading
   its sort value via the `SortColumn.Selector`. The `+1`-th row is dropped.
7. **Count** — separate `COUNT(*)` over **the same filtered query before step 3**
   (search applied, no cursor / no order / no take). Run in parallel with the
   page query via `Task.WhenAll` to halve latency.
8. **Return** `KeysetPage<TDto>`.

**Caller responsibility** — supply the `baseQuery` already filtered for tenancy,
soft-delete (or `.IgnoreQueryFilters()` when `IncludeDeleted=true`), or any
domain-level predicates. The helper does pagination plumbing, nothing else.

### Optional validator base

`KeysetQueryOptionsValidator<TEntity>` — FluentValidation base class that
enforces the universal rules. Subclass it per list to add entity-specific rules:

- `Search`: optional; if set, `≤ 200` chars.
- `SortBy`: optional; if set, must be in `sortMap.AllowedKeys` (case-insensitive).
- `SortDir`: enum, model-bound, valid values only.
- `PageSize`: `1 ≤ value ≤ 200`.
- `Cursor`: optional; if set, `KeysetCursor.TryDecode` must succeed.
- `IncludeDeleted`: bool, no rule.

Each list still has its own concrete `GetXxxQuery` (record), validator, and
handler — same vertical-slice shape as other features. The base class is just
shared validation rules.

### Wire the validator into the existing pipeline

`ValidationBehavior` already runs FluentValidation through the dispatcher
(see [the recent dispatcher refactor](#)). Registering a
`KeysetQueryOptionsValidator<TEntity>`-derived validator picks up automatically.
No new pipeline behavior needed.

### Files unchanged

- `Tsz.Infrastructure/Cqrs/*` — works as-is. Generic `IRequestHandler<TQuery, KeysetPage<TDto>>`.
- `Tsz.Infrastructure/Errors/*` — `ValidationException` is the failure path.

---

## Primitives — frontend

Live under `packages/web/src/components/list/` (next to `components/form/`).

### Files

```
packages/web/src/lib/
  use-list-query.ts          — wraps useInfiniteQuery, returns flat items +
                               sentinel helper + sort/search state setters
  use-infinite-scroll-sentinel.ts
                             — IntersectionObserver hook returning a callback ref

packages/web/src/components/list/
  list-toolbar.tsx           — <Input> + optional <Switch> + total count <span>
  sortable-header.tsx        — <Button variant="ghost"> column header that
                               cycles asc → desc → asc + arrow indicator
  infinite-table.tsx         — wraps shadcn <Table>; injects the sentinel row
                               at the bottom; shows loading / empty / error
                               states uniformly
  list-shell.tsx             — convenience composition: toolbar + table + state;
                               opt-in (callers can use the pieces directly)

packages/web/src/api/
  pagination.ts              — shared types: KeysetPage<T>, KeysetQueryParams
```

### Types — `api/pagination.ts`

```ts
export type KeysetPage<T> = {
  items: T[];
  nextCursor: string | null;
  total: number;
};

export type SortDir = 'asc' | 'desc';

export type KeysetQueryParams<TSortKey extends string = string> = {
  search?: string;
  sortBy?: TSortKey;
  sortDir?: SortDir;
  pageSize?: number;
  cursor?: string;
  includeDeleted?: boolean;
};
```

Each list's API wrapper narrows `TSortKey` to its allowed values, e.g.
`KeysetQueryParams<'name' | 'email' | 'role'>` for users.

### `useListQuery`

```ts
export function useListQuery<TItem, TSortKey extends string>(opts: {
  queryKey: readonly unknown[];                                     // ['users']
  fetcher: (params: KeysetQueryParams<TSortKey>) => Promise<KeysetPage<TItem>>;
  defaultSort: { by: TSortKey; dir: SortDir };
  pageSize?: number;
});
```

Returns:

```ts
{
  items: TItem[];                       // flattened across pages
  total: number | undefined;            // from first page
  search: string; setSearch: (v: string) => void;
  sortBy: TSortKey; sortDir: SortDir;
  setSort: (by: TSortKey) => void;      // cycles same column asc↔desc
  includeDeleted: boolean; setIncludeDeleted: (v: boolean) => void;
  sentinelRef: (node: HTMLElement | null) => void;
  isLoading: boolean;
  isFetchingNextPage: boolean;
  error: unknown;
}
```

Internally:

- Holds `search`, `sortBy`, `sortDir`, `includeDeleted` in component state.
- Uses `useDeferredValue` to debounce `search` into the query key.
- Builds a `useInfiniteQuery({ queryKey: [...queryKey, params], queryFn, getNextPageParam: last => last.nextCursor ?? undefined, initialPageParam: undefined })`.
- Flattens `data?.pages.flatMap(p => p.items)`.
- Wires `useInfiniteScrollSentinel` to call `fetchNextPage()` when the sentinel
  intersects and `hasNextPage && !isFetchingNextPage`.

### `useInfiniteScrollSentinel`

Tiny hook (~15 lines) wrapping IntersectionObserver. Decoupled so consumers
can also wire it manually if they want different `rootMargin`.

```ts
export function useInfiniteScrollSentinel(opts: {
  enabled: boolean;
  onIntersect: () => void;
  rootMargin?: string;                 // default '200px'
}): (node: HTMLElement | null) => void;
```

### `<SortableHeader>`

```tsx
<SortableHeader
  label="Name"
  sortKey="name"
  currentSortBy={sortBy}
  currentSortDir={sortDir}
  onSort={setSort}
/>
```

Renders a ghost `<Button>` with a chevron indicator (▲ / ▼) when the column is
active. Cycles `asc → desc` on each click of the same column; sets new column
to `asc` when switching.

### `<ListToolbar>`

```tsx
<ListToolbar
  search={search}
  onSearchChange={setSearch}
  total={total}
  toggle={                                  /* optional, e.g. active filter */
    { label: 'Active', checked: !includeDeleted, onChange: v => setIncludeDeleted(!v) }
  }
/>
```

Lays out: `<Input placeholder="Filter…">` on the left, optional `<Switch>` +
label in the middle, count `<span>{total ?? '—'} items</span>` right.

### `<InfiniteTable>`

```tsx
<InfiniteTable
  items={items}
  rowKey={u => u.id}
  isLoading={isLoading}
  isFetchingNextPage={isFetchingNextPage}
  error={error}
  emptyState="No users match."
  sentinelRef={sentinelRef}
  onRowClick={u => navigate({ to: '/admin/users/$id', params: { id: u.id } })}
>
  {{
    head: (
      <>
        <SortableHeader ... />
        <SortableHeader ... />
      </>
    ),
    row: (u) => (
      <>
        <TableCell>{u.firstName} {u.lastName}</TableCell>
        <TableCell>{u.email}</TableCell>
      </>
    ),
  }}
</InfiniteTable>
```

Renders the shadcn `<Table>`, the sentinel `<tr>`, loading/empty/error rows,
and the row click handler. The split-children `{ head, row }` keeps the row
renderer pure and the head out of the row loop.

### `<ListShell>` (convenience)

A thin wrapper that composes `<ListToolbar>` + `<InfiniteTable>` + an "Add"
button. Optional — callers wanting custom layout drop down to the pieces.

---

## First consumer: user list

Per [../../users/plan-list.md](../../users/plan-list.md). After this generic
plan lands, that doc gets rewritten to "use the listBase primitives" — it
becomes mostly a config: `SortMap<User>`, searchable columns, validator
extras, column renderers. Behavior expectations (screenshot, edge cases,
manual smoke checklist) stay in the user-list doc.

---

## Steps

1. **API** — Add `Common/Pagination/{KeysetCursor, KeysetPage, KeysetQueryOptions,
   SortMap, KeysetQueryableExtensions}.cs` in `Tsz.Infrastructure`. Pure
   library code, no module dependencies.
2. **API tests** — `KeysetCursorTests` (encode round-trip, version bump,
   garbage input), `KeysetQueryableExtensionsTests` (against an in-memory
   `IQueryable` over a fixture entity — covers search, sort asc/desc, cursor
   continuation, total count, tiebreaker, page edge `LIMIT+1`).
3. **API** — `KeysetQueryOptionsValidator<TEntity>` base + tests covering each
   universal rule.
4. **Web** — `api/pagination.ts` types; `lib/use-list-query.ts`;
   `lib/use-infinite-scroll-sentinel.ts`; `components/list/{sortable-header,
   list-toolbar, infinite-table, list-shell}.tsx`. Storybook is not set up so
   verification is via the first consumer (user list).
5. **Migrate user list** per [../../users/plan-list.md](../../users/plan-list.md).
   That plan's "Steps" section gets simplified once the primitives are in
   place: define `SortMap<User>`, list searchable columns, write a thin
   validator subclass, supply column renderers.
6. **Manual smoke test** the user list using the checklist in plan-list.md.
7. **Optional** — once a second consumer (contracts, customers, timesheets…)
   shows up, revisit whether `<ListShell>` is pulling its weight or if the
   pieces alone are enough.

---

## Tests

| Layer  | Target                                           | Where                                       |
| ------ | ------------------------------------------------ | ------------------------------------------- |
| Unit   | `KeysetCursor` encode/decode/versioning          | `Tsz.Api.Tests/Common/Pagination/`          |
| Unit   | `KeysetQueryableExtensions` against fixture data | `Tsz.Api.Tests/Common/Pagination/`          |
| Unit   | `KeysetQueryOptionsValidator<T>` rules           | `Tsz.Api.Tests/Common/Pagination/`          |
| Integ. | One real endpoint exercising paging end-to-end   | `Tsz.Api.Tests.Integration/UserEndpointsTests.cs` (per user-list plan) |
| Web    | None at the primitive level                      | Covered by manual smoke + first consumer    |

The infinite-scroll sentinel and `useListQuery` are thin enough that a Vitest
test against `useInfiniteScrollSentinel` adds little — the IntersectionObserver
contract is the integration surface, and that needs JSDOM gymnastics or a real
browser. Defer until a regression demands it.

---

## Edge cases the generic plumbing handles

- **Concurrent inserts mid-scroll** — a new row inserted between page fetches
  may appear or not depending on its sort value vs. the cursor. Acceptable;
  no snapshot needed.
- **Partial last page** — `nextCursor` is `null` when `Items.Count < pageSize`.
  `useInfiniteQuery.hasNextPage` flips false; sentinel goes inert.
- **`pageSize` change mid-session** — different cache key, full reset. Fine.
- **Cursor `v` mismatch** — `TryDecode` returns false → caller returns `400`.
  Client-side: catch `400` on infinite scroll, drop pages, refetch from start.
  Out of scope for this pass; treat as "user reloads."
- **Lexicographic enum sort** — `Role` ordered as `Admin / ClientManager / User`.
  Document per-consumer; add a `RoleSortKey` mapping only if product asks.
- **Empty result** — `total: 0, items: [], nextCursor: null`.

## Edge cases out of scope

- Concurrent **deletes** that invalidate a cursor's `(sortValue, id)` row —
  the next page starts from the next greater row regardless, which is correct;
  the user just sees a smaller list. No special handling needed.
- Server-side cap on `Total` (e.g. "999+") if `COUNT(*)` ever becomes a real
  cost. Don't pre-optimise.

## Assumptions

- All sort columns currently in scope are `string`, `int`, or `DateTime`.
  `JsonElement` round-trips all three cleanly. When the first `decimal` /
  `Guid` / custom-struct sort column shows up, the helper's CLR-cast switch
  picks it up via the existing `SortColumn.ClrType`.
- SQLite is the only provider. `LIKE` + `LOWER` works without `COLLATE NOCASE`
  in most cases; if a column needs explicit case-insensitivity we'll add
  `EF.Functions.Collate(col, "NOCASE")` per-column in the searchable list.
- The single-component-state model (search/sort/scroll in the route file) is
  fine for v1. If a second consumer wants URL persistence, lift state into
  TanStack Router `useSearch` then — same component API, different state
  source.
- shadcn `Switch` will be added when the first consumer ships an active toggle.
