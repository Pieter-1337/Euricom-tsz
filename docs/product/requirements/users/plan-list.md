# Plan: User list — server-side search, sort, active toggle, total count, keyset pagination, infinite scroll

## Goal

Replace the current dumb `GET /api/users` + dumb table with a server-driven list matching `admin-user-list.png`:

- Free-text filter (case-insensitive contains over Name + Email).
- Active toggle: default ON = exclude soft-deleted, OFF = include them. No new model field — bypass the EF `HasQueryFilter` when OFF.
- Total user count label ("X users").
- Sortable columns (Name, Email, Role) — single-column, asc/desc toggle.
- **Keyset (a.k.a. seek) pagination** — opaque cursor, no `OFFSET/LIMIT`.
- Infinite scroll on the client (TanStack Query `useInfiniteQuery` + IntersectionObserver).

## Non-goals

- New `User` fields (no Personnel Number, First/Last/Nickname, etc.). The single `Name` + `Email` + `Role` we already have are the only sortable/searchable columns. Leaves plan is separate.
- Full-text search (SQLite FTS5, trigram, etc.). Default is `LOWER(col) LIKE '%term%'`. Trivially swappable to FTS5 in a future plan if it ever gets slow.
- Server-driven multi-column sort. Single sort column + stable `Id` tiebreaker covers the screenshot.

## Search backend choice

Asked: "canonical search options here?" — answer for context:

| Option                       | Verdict                                                                                                              |
| ---------------------------- | -------------------------------------------------------------------------------------------------------------------- |
| `EF.Functions.Like` + `LOWER`| **Chosen.** Works on any provider, no schema cost, sufficient at thousands of rows.                                  |
| SQLite FTS5                  | Real full-text search. Needs a virtual table + triggers + raw SQL (EF Core has no first-class support). Overkill now.|
| Postgres tsvector / pg_trgm  | N/A — we're on SQLite.                                                                                               |
| Cosmos DB `$search`          | N/A — different engine.                                                                                              |
| Azure AI Search              | N/A — separate infra, monthly cost.                                                                                  |

`EF.Functions.Like(EF.Functions.Collate(u.Name, "NOCASE"), $"%{term}%")` keeps it readable; on SQLite this lowers both sides automatically with the `NOCASE` collation. No SQL injection — EF parameterises.

## API contract

### Request

```
GET /api/users
  ?search=<text>              optional, case-insensitive contains across Name + Email
  &sortBy=<col>               optional; one of: name | email | role. Default: name.
  &sortDir=<asc|desc>         optional; default: asc.
  &pageSize=<n>               optional; default 50, capped 200.
  &cursor=<base64>            optional; opaque token from a prior response.
  &includeDeleted=<bool>      optional; default false.
```

Behaviour:
- Empty / missing `search` → no `LIKE` filter applied.
- `sortBy` is validated against an allow-list — anything else → `400 ValidationProblem`.
- `cursor` is opaque to the client (base64-url JSON `{ v, sortValue, id }`). On decode failure → `400`.
- `includeDeleted = true` bypasses the soft-delete query filter (via `.IgnoreQueryFilters()` on the query, then re-applying the search/sort/cursor parts manually).

### Response

```jsonc
{
  "items": [ /* UserDto[] */ ],
  "nextCursor": "eyJ2IjoxLCJzb3J0VmFsdWUiOiJBbGljZSIsImlkIjoiLi4uIn0",  // null when last page
  "total": 137                                                            // matches the filter, ignores cursor
}
```

`total` is a separate `COUNT(*)` query over the same `WHERE` (search + soft-delete) — no cursor predicate applied. For a few-hundred-row table this is negligible; if it ever isn't, switch to lazy/approximate later.

### Status codes

- `200 OK` always, with `items: []` and `total: 0` when no match.
- `400` on bad `sortBy`, bad `sortDir`, malformed `cursor`, `pageSize` out of bounds.
- `403` for non-admins (existing `RequireAdmin`).

## Keyset pagination — design

### Why keyset, not offset

Offset paging re-scans skipped rows (cost grows with page index) and gives inconsistent windows under concurrent inserts/deletes. Keyset uses a stable index seek: cost is O(pageSize) regardless of position.

### Cursor shape

Opaque base64-url of:

```json
{ "v": 1, "sortValue": <string|number>, "id": "<guid>" }
```

- `v`: schema version; bump on breaking change.
- `sortValue`: the value of the sort column on the **last row** of the previous page. For `role` we encode the enum's **string** form (matches the API wire shape).
- `id`: the `Id` of the same last row — used as the strict tiebreaker.

Encoded with `JsonSerializer` → UTF-8 bytes → `Base64UrlEncoder.Encode`. Decoded inverse-direction. The cursor never leaks server-side state (no signed token needed; admin-only endpoint, no security gain from signing).

### Page query

For `sortDir = asc`:

```sql
WHERE [filters]
  AND (sortCol > @sortValue
    OR (sortCol = @sortValue AND Id > @id))
ORDER BY sortCol ASC, Id ASC
LIMIT @pageSize + 1
```

For `desc`: flip `>` → `<` and `ASC` → `DESC`.

The `+1`: we fetch one extra row to know whether `nextCursor` should be set. If `LIMIT+1` came back, the next cursor is built from the `pageSize`-th row and the `+1`-th row is dropped from the response.

### Sort allow-list + projection map

```csharp
private static readonly IReadOnlyDictionary<string, Expression<Func<User, object>>> SortMap = new Dictionary<string, Expression<Func<User, object>>>(StringComparer.OrdinalIgnoreCase)
{
    ["name"]  = u => u.Name,
    ["email"] = u => u.Email,
    ["role"]  = u => u.Role,           // enum stored as string — sorts lexicographically; documented quirk
};
```

`Role` sorts lexicographically by enum-name (`Admin` < `ClientManager` < `User`). Document this and accept it; if product wants a custom order (e.g. Admin first always), add a `RoleSortKey` mapping later.

### Encoded cursor value type

`sortValue` is always serialised as the underlying CLR type (`string` for Name/Email/Role). EF translation handles the comparison without `CAST`. For numeric sorts (future), the JSON number → `decimal`/`int` cast in the decoder needs to be type-aware — out of scope for v1 since all three sort columns are `string`.

## File layout — API

```
Common/
  Pagination/
    KeysetCursor.cs              ← record { int V, JsonElement SortValue, Guid Id } + Encode/TryDecode
    KeysetPage.cs                ← record<T> { IReadOnlyList<T> Items, string? NextCursor, int Total }
    KeysetQueryOptions.cs        ← record { string? Search, string SortBy, SortDirection SortDir, int PageSize, string? Cursor, bool IncludeDeleted }

Modules/Users/
  Features/
    GetUsers.cs                  ← MODIFIED: switch from List<UserDto> to KeysetPage<UserDto>
```

`GetUsers` becomes a thin orchestrator:

1. Validate options (FluentValidation).
2. Build base `IQueryable<User>` from `IRepository<User>`. Apply `IgnoreQueryFilters` if `IncludeDeleted` true; re-apply `DeletedAt == null` filter manually only when not including deleted (the `HasQueryFilter` does this for the default path).
3. Apply `LOWER(Name) LIKE` / `LOWER(Email) LIKE` (combined `||`) when `Search` non-empty.
4. Apply cursor predicate if cursor non-null.
5. Apply `ORDER BY sortCol [dir], Id [dir]`.
6. `Take(pageSize + 1)`.
7. Materialise + slice + build `NextCursor`.
8. Run the `COUNT(*)` query (steps 2 + 3 only, no cursor / order / take).
9. Return `KeysetPage<UserDto>`.

`UserEndpoints.cs`: the `MapGet("/")` lambda binds `[AsParameters] GetUsersQuery` so all query params come in as a single record. Add `ValidationFilter<GetUsersQuery>`.

## File layout — Web

```
src/api/
  users.ts                       ← MODIFIED: getUsers returns KeysetPage<UserDto>;
                                   new exported types KeysetPage<T>, ListUsersParams

src/routes/_protected/admin/users/
  index.tsx                      ← REWRITTEN per the screenshot
```

### `getUsers` wrapper

```ts
export type ListUsersParams = {
  search?: string;
  sortBy?: 'name' | 'email' | 'role';
  sortDir?: 'asc' | 'desc';
  pageSize?: number;
  cursor?: string;
  includeDeleted?: boolean;
};

export type KeysetPage<T> = {
  items: T[];
  nextCursor: string | null;
  total: number;
};

export async function getUsers(params: ListUsersParams): Promise<KeysetPage<UserDto>> { /* ... */ }
```

### `_protected/admin/users/index.tsx` shape

State (single component, no global store):

- `search: string` — debounced 300ms via small inline hook or `useDeferredValue`.
- `sortBy: 'name' | 'email' | 'role'`, `sortDir: 'asc' | 'desc'` — single-column.
- `includeDeleted: boolean`.

Query: `useInfiniteQuery({ queryKey: ['users', { search, sortBy, sortDir, includeDeleted }], queryFn: ({ pageParam }) => getUsers({ ..., cursor: pageParam }), getNextPageParam: lastPage => lastPage.nextCursor ?? undefined, initialPageParam: undefined })`.

UI bits (all shadcn unless noted):

- Header row: `<Button>Add</Button>` (links to `/admin/users/new`).
- Toolbar row: `<Input placeholder="Filter…">`, `<Switch checked={!includeDeleted} onCheckedChange={v => setIncludeDeleted(!v)}>` labelled "Active", `<span>{data?.pages[0]?.total ?? '—'} users</span>` aligned right.
- `<Table>` with three sortable headers. Each header is a `<Button variant="ghost">` that cycles `asc → desc → asc` on click and shows a triangle indicator on the active column. Rows clickable → `/admin/users/$id`.
- Sentinel `<tr>` at the bottom of `<TableBody>` with an IntersectionObserver hook. When it intersects + `hasNextPage` + `!isFetchingNextPage` → `fetchNextPage()`.
- Loading: spinner row at bottom while `isFetchingNextPage`. Empty state: "No users match." Error: shadcn `<Alert variant="destructive">`.

Sentinel hook (small enough to inline in the file, not a util):

```tsx
const sentinelRef = useRef<HTMLTableRowElement>(null);
useEffect(() => {
  if (!sentinelRef.current || !hasNextPage) return;
  const obs = new IntersectionObserver(
    ([entry]) => entry.isIntersecting && !isFetchingNextPage && fetchNextPage(),
    { rootMargin: '200px' },
  );
  obs.observe(sentinelRef.current);
  return () => obs.disconnect();
}, [hasNextPage, isFetchingNextPage, fetchNextPage]);
```

`rootMargin: 200px` pre-fetches before the user hits the literal bottom — smoother feel.

### Debounce

Inline minimum:

```tsx
const [searchInput, setSearchInput] = useState('');
const search = useDeferredValue(searchInput);
```

`useDeferredValue` is enough; if it feels too eager, swap for a hand-rolled `setTimeout` debounce. Don't pull in lodash.

## Validation

`GetUsersQueryValidator` (FluentValidation):

- `Search`: optional; if set, max length 200 (defensive — `LIKE '%200chars%'` is fine, but reject obvious abuse).
- `SortBy`: optional; if set, must be in `{name, email, role}` (case-insensitive). Default applied in handler when null.
- `SortDir`: optional; if set, must be `asc` or `desc`.
- `PageSize`: optional; if set, 1 ≤ `PageSize` ≤ 200.
- `Cursor`: optional; if set, decodes successfully (validator owns the decode-or-fail, handler reuses the parsed `KeysetCursor`).
- `IncludeDeleted`: optional; bool — model binder handles it.

## Tests

### Unit (`Tsz.Api.Tests`)

- `KeysetCursorTests` — Encode round-trips Decode for `string` / `int` / `Guid` `sortValue`s. Garbage input → `TryDecode` returns false. Forward-compat: unknown version `v: 99` → false.
- `GetUsersHandlerTests` (rewrite):
  - Empty DB → `Items: [], NextCursor: null, Total: 0`.
  - 50 users, default sort → first page returns 50, `NextCursor` null, `Total: 50`.
  - 51 users, `pageSize=50` → first page 50 + non-null cursor; second page 1 + null cursor; concatenation equals full set in order.
  - Search "ali" → only matching rows, case-insensitive, both Name and Email checked.
  - `SortBy=email, SortDir=desc` → reversed order, cursor still works.
  - Soft-deleted rows: hidden by default, visible when `IncludeDeleted=true`, `Total` reflects the toggle.
  - Tiebreaker: two users with identical Name → deterministic order by Id; cursor never repeats or skips.
- `GetUsersQueryValidatorTests` — every validation rule.

### Integration (`Tsz.Api.Tests.Integration`)

- `UserEndpointsTests` — add cases:
  - Non-admin → 403 on `GET /api/users` (existing, keep).
  - Seed 23 users → page through with `pageSize=10` → 3 requests, totals 23, items align.
  - `?search=` + admin → filters server-side, body has matching rows + correct `total`.
  - `?sortBy=email&sortDir=desc` → returned order matches expected.
  - `?includeDeleted=true` after soft-delete → soft-deleted user appears, `total` increments.
  - Bad `sortBy=ssn` → 400.
  - Bad `cursor=garbage` → 400.

## Smoke test plan (manual)

1. Sign in as admin. List shows current users (just Pieter in dev).
2. Add ~10 users via `/admin/users/new` (or seed for the smoke test) → list shows all + "10 users" count.
3. Type "pie" in filter → list narrows to Pieter, count updates → clear → count back.
4. Click "Email" column header → rows reorder, indicator on Email. Click again → desc. Click "Name" → back to Name asc.
5. Set `pageSize=3` (via URL or temporarily lower default) → scroll → next pages load without flicker, no row dupes/skips.
6. Soft-delete a user → list loses the row, count drops. Toggle Active off → row reappears greyed-ish (optional styling), count rises.
7. Reload mid-scroll → reset to first page (expected behaviour for v1; no URL persistence).

## Edge cases & non-goals

- **Concurrent inserts mid-scroll.** A new user inserted between page fetches might appear or not depending on its sort value vs the cursor. Acceptable for an admin list; we don't snapshot.
- **Sort by Role surfaces "Admin / ClientManager / User"** lexicographically — confusing. Add to the page's tiny help tooltip if anyone complains; otherwise leave.
- **Empty `nextCursor` on partial page.** Handler returns `null` when `Items.Count < pageSize` to short-circuit IntersectionObserver. `useInfiniteQuery.hasNextPage` flips false.
- **Page-size churn.** Changing `pageSize` mid-session resets the query (new cache key). Fine.
- **URL persistence of search/sort.** Out of scope. If we want shareable filtered URLs, lift state into TanStack Router `useSearch`; one-day add.
- **Multi-column sort, faceted filters, saved views.** All out of scope; this is the "table that works" plan, not a data-grid library.
- **Bulk actions.** Out of scope.

## Steps

1. **API** — Add `Common/Pagination/{KeysetCursor,KeysetPage,KeysetQueryOptions}.cs`. Unit-tested in isolation.
2. **API** — Rewrite `GetUsers` slice (query record, validator, handler) returning `KeysetPage<UserDto>`. Update `UserEndpoints` to `[AsParameters]`-bind and apply `ValidationFilter`.
3. **API** — Unit + integration tests per the matrix.
4. **Regen** — `bun --filter web gen:api`.
5. **Web** — Update `src/api/users.ts`: `getUsers` signature + types.
6. **Web** — Rewrite `_protected/admin/users/index.tsx`: toolbar, sortable headers, total label, infinite scroll sentinel, debounced search, active toggle.
7. **Smoke test** the manual checklist.

## Open questions deferred

- If/when we add columns to `User` (when "leaves" plan or another lands), revisit the `SortMap` — adding a new sortable column is one allow-list entry + one cursor-value type if it isn't `string`.
- Server-side row count truncation (e.g. show "999+") only if `COUNT(*)` becomes measurable. Don't pre-optimise.
