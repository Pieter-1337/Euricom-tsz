# Plan: User list — server-side search, sort, active toggle, total count, keyset pagination, infinite scroll

Consumes the listBase primitives defined in [../generics/list/plan.md](../generics/list/plan.md).

## Goal

Replace the current dumb `GET /api/users` + dumb table with a server-driven list matching `admin-user-list.png`:

- Free-text filter (case-insensitive contains over FirstName + LastName + Email).
- Active toggle: default ON = exclude soft-deleted, OFF = include them. No new model field — bypass the EF `HasQueryFilter` when OFF.
- Total user count label ("X users").
- Sortable columns (Name, Email, Role) — single-column, asc/desc toggle.
- Keyset pagination with infinite scroll (see listBase plan).

## Non-goals

- New `User` fields (no Personnel Number, First/Last/Nickname, etc.). FirstName, LastName, Email, Role are the only sortable/searchable columns. Leaves plan is separate.
- Full-text search. The generic `LOWER(col) LIKE '%term%'` is sufficient.

## API contract

Generic shape is `GET /api/users?search=...&sortBy=...&sortDir=...&pageSize=...&cursor=...&includeDeleted=...` per the listBase plan. User-specific details:

- **Searchable columns**: `FirstName`, `LastName`, `Email` (combined case-insensitive `LIKE`).
- **Sortable columns (SortMap)**: `name` (FirstName+LastName), `email`, `role`. Default: `name` asc.
- **Role sort quirk**: Enum values sort lexicographically as `Admin < ClientManager < User`. Document in UI tooltips if needed; no custom sort key for v1.
- **Response shape**: `KeysetPage<UserDto>` (see listBase plan).
- **Status codes**: `200 OK` (with empty items on no match), `400` on validation failure, `403` for non-admins.

## User-specific configuration

### SortMap<User>

```csharp
private static readonly SortMap<User> Sort = new(
    new SortColumn<User>("name", u => u.FirstName + " " + u.LastName, typeof(string)),
    new SortColumn<User>("email", u => u.Email, typeof(string)),
    new SortColumn<User>("role", u => u.Role, typeof(string))
);
```

Default sort: `name` asc.

### Searchable columns

```csharp
private static readonly Expression<Func<User, string>>[] SearchableColumns = new[]
{
    (Expression<Func<User, string>>)(u => u.FirstName),
    (Expression<Func<User, string>>)(u => u.LastName),
    (Expression<Func<User, string>>)(u => u.Email),
};
```

Search is case-insensitive `LIKE` over all three (combined with `||` in the WHERE). See listBase plan for the `ToKeysetPageAsync` mechanics.

## Backend implementation

### `Modules/Users/Features/GetUsers.cs`

```csharp
public sealed record GetUsersQuery(
    string? Search,
    string? SortBy,
    SortDirection SortDir,
    int PageSize,
    string? Cursor,
    bool IncludeDeleted)
    : KeysetQueryOptions(Search, SortBy, SortDir, PageSize, Cursor, IncludeDeleted);

public sealed class GetUsersQueryValidator : KeysetQueryOptionsValidator<GetUsersQuery>
{
    public GetUsersQueryValidator(SortMap<User> sortMap) : base(sortMap)
    {
        // Add user-specific rules beyond the generic KeysetQueryOptionsValidator<T> here if needed.
        // For now, no extras.
    }
}

public sealed class GetUsersHandler(IUnitOfWork uow)
    : IQueryHandler<GetUsersQuery, KeysetPage<UserDto>>
{
    private static readonly SortMap<User> Sort = new(
        new SortColumn<User>("name", u => u.FirstName + " " + u.LastName, typeof(string)),
        new SortColumn<User>("email", u => u.Email, typeof(string)),
        new SortColumn<User>("role", u => u.Role, typeof(string)));

    private static readonly Expression<Func<User, string>>[] SearchableColumns = new[]
    {
        (Expression<Func<User, string>>)(u => u.FirstName),
        (Expression<Func<User, string>>)(u => u.LastName),
        (Expression<Func<User, string>>)(u => u.Email),
    };

    public async Task<KeysetPage<UserDto>> HandleAsync(GetUsersQuery query, CancellationToken ct = default)
    {
        var baseQuery = uow.RepositoryFor<User>().GetQueryable();
        if (query.IncludeDeleted)
            baseQuery = baseQuery.IgnoreQueryFilters();

        return await baseQuery.ToKeysetPageAsync(
            query,
            Sort,
            SearchableColumns,
            UserDto.Project,
            ct);
    }
}
```

### `Modules/Users/UserEndpoints.cs`

```csharp
.MapGet("/", GetUsersEndpoint)
    .RequireAdmin()
    .WithName("GetUsers")
    .WithOpenApi();

private static Task<KeysetPage<UserDto>> GetUsersEndpoint(
    [AsParameters] GetUsersQuery query,
    IRequestHandler<GetUsersQuery, KeysetPage<UserDto>> handler,
    CancellationToken ct) => handler.HandleAsync(query, ct);
```

The `[AsParameters]` binds all query params to the `GetUsersQuery` record; `ValidationFilter` runs automatically via the CQRS pipeline.

## Frontend implementation

### `src/api/users.ts`

```ts
import { KeysetQueryParams, KeysetPage } from './pagination';
import { UserDto } from './schema';

export type ListUsersParams = KeysetQueryParams<'name' | 'email' | 'role'>;

export async function getUsers(params: ListUsersParams): Promise<KeysetPage<UserDto>> {
  const query = new URLSearchParams(
    Object.entries(params).filter(([, v]) => v !== undefined) as [string, string][]
  );
  const res = await fetch(`/api/users?${query}`);
  if (!res.ok) throw new Error(`Failed to fetch users: ${res.status}`);
  return res.json();
}
```

Types `KeysetQueryParams<T>` and `KeysetPage<T>` are defined in `src/api/pagination.ts` (shared across all lists).

### `src/routes/_protected/admin/users/index.tsx`

```tsx
import { useListQuery } from '@/lib/use-list-query';
import { ListToolbar } from '@/components/list/list-toolbar';
import { InfiniteTable } from '@/components/list/infinite-table';
import { SortableHeader } from '@/components/list/sortable-header';
import { getUsers } from '@/api/users';
import { useNavigate } from '@tanstack/react-router';

export function UsersPage() {
  const navigate = useNavigate();
  const {
    items,
    total,
    search,
    setSearch,
    sortBy,
    sortDir,
    setSort,
    includeDeleted,
    setIncludeDeleted,
    sentinelRef,
    isLoading,
    isFetchingNextPage,
    error,
  } = useListQuery({
    queryKey: ['users'],
    fetcher: getUsers,
    defaultSort: { by: 'name', dir: 'asc' },
  });

  return (
    <div className="space-y-4">
      <ListToolbar
        search={search}
        onSearchChange={setSearch}
        total={total}
        toggle={{ label: 'Active', checked: !includeDeleted, onChange: (v) => setIncludeDeleted(!v) }}
      />
      <InfiniteTable
        items={items}
        rowKey={(u) => u.id}
        isLoading={isLoading}
        isFetchingNextPage={isFetchingNextPage}
        error={error}
        emptyState="No users match."
        sentinelRef={sentinelRef}
        onRowClick={(u) => navigate({ to: '/admin/users/$id', params: { id: u.id } })}
      >
        {{
          head: (
            <>
              <SortableHeader
                label="Name"
                sortKey="name"
                currentSortBy={sortBy}
                currentSortDir={sortDir}
                onSort={setSort}
              />
              <SortableHeader
                label="Email"
                sortKey="email"
                currentSortBy={sortBy}
                currentSortDir={sortDir}
                onSort={setSort}
              />
              <SortableHeader
                label="Role"
                sortKey="role"
                currentSortBy={sortBy}
                currentSortDir={sortDir}
                onSort={setSort}
              />
            </>
          ),
          row: (u) => (
            <>
              <TableCell>{u.firstName} {u.lastName}</TableCell>
              <TableCell>{u.email}</TableCell>
              <TableCell>{u.role}</TableCell>
            </>
          ),
        }}
      </InfiniteTable>
    </div>
  );
}
```

All state, search debouncing, infinite-scroll sentinel wiring, and loading states are handled by `useListQuery` and `<InfiniteTable>` (see listBase plan).

## Validation

`GetUsersQueryValidator` inherits all generic rules from `KeysetQueryOptionsValidator<GetUsersQuery>` (see listBase plan):

- `Search`: max 200 chars if set.
- `SortBy`: must be in `{name, email, role}` if set.
- `SortDir`: valid enum value if set.
- `PageSize`: 1–200 if set.
- `Cursor`: decodes successfully if set.

No user-specific validator extras needed for v1.

## Tests

### Unit tests (`Tsz.Api.Tests`)

- `GetUsersHandlerTests`:
  - Empty DB → `Items: [], NextCursor: null, Total: 0`.
  - 50 users, default sort (name asc) → first page returns 50, `NextCursor` null, `Total: 50`.
  - 51 users, `pageSize=50` → first page 50 + non-null cursor; second page 1 + null cursor.
  - Search "john" → matches FirstName, LastName, and Email case-insensitively.
  - `SortBy=email, SortDir=desc` → reversed by email, cursor still works.
  - Soft-deleted rows: hidden by default, visible when `IncludeDeleted=true`, `Total` reflects the toggle.
  - Tiebreaker: two users with identical names → deterministic order by `Id`.
- `GetUsersQueryValidatorTests` — inherits generic rules, all pass via base class.

### Integration tests (`Tsz.Api.Tests.Integration`)

- `UserEndpointsTests`:
  - Non-admin → 403 on `GET /api/users`.
  - Seed 23 users → page through with `pageSize=10` → 3 requests, items total 23.
  - Search matches FirstName + LastName + Email, case-insensitive.
  - `SortBy=email&SortDir=desc` → correct order.
  - `IncludeDeleted=true` after soft-delete → deleted user appears, `total` rises.
  - Bad `sortBy` → 400 (validation).
  - Bad `cursor` → 400 (validation).

See listBase plan for generic keyset/cursor tests (`KeysetCursorTests`, `KeysetQueryableExtensionsTests`).

## Smoke test (manual)

1. Sign in as admin. List shows current users (just Pieter in dev).
2. Add ~10 users via `/admin/users/new` → list shows all + "10 users" count label.
3. Type "john" in the search box → list narrows to matching FirstName/LastName/Email (case-insensitive), count updates.
4. Clear search → list back to full set.
5. Click "Email" column header → rows reorder by email asc, chevron indicator appears. Click again → desc. Click "Name" → back to name asc.
6. Soft-delete a user (via admin panel or DB) → list loses the row, count drops.
7. Toggle "Active" off → soft-deleted user reappears, count rises. Toggle back on → hidden again.
8. Scroll to bottom → sentinel row triggers next page fetch, no dupes or skips.
9. Reload mid-scroll → reset to first page (expected; no URL persistence in v1).

## Edge cases (user-specific)

- **Role sort order.** Enum values sort lexicographically: `Admin < ClientManager < User`. Document in tooltips if needed; no custom mapping for v1.
- **FirstName + LastName "Name" sort.** Concatenates as `FirstName + " " + LastName` for the cursor's sort value. Ties break by `Id` (see listBase plan).

All other edge cases (concurrent inserts, partial pages, page-size churn, URL persistence) are handled by the generic listBase primitives or explicitly deferred.

## Steps

Assumes listBase primitives are already in place (see [../generics/list/plan.md](../generics/list/plan.md)).

1. **API** — Define `SortMap<User>` and `SearchableColumns` (FirstName, LastName, Email).
2. **API** — Rewrite `GetUsers.cs`: `GetUsersQuery` subclasses `KeysetQueryOptions`, validator subclasses `KeysetQueryOptionsValidator<T>`, handler calls `ToKeysetPageAsync`.
3. **API** — Update `UserEndpoints.cs` to bind with `[AsParameters]` and return `KeysetPage<UserDto>`.
4. **API** — Unit + integration tests (GetUsers handler + validator + endpoint).
5. **Regen** — `bun --filter web gen:api` to sync schema types.
6. **Web** — Update `src/api/users.ts`: `getUsers` signature and `ListUsersParams` type.
7. **Web** — Rewrite `_protected/admin/users/index.tsx` using `useListQuery`, `<ListToolbar>`, `<InfiniteTable>`, `<SortableHeader>`.
8. **Smoke test** the manual checklist.

## Open questions deferred

- **New User fields.** Adding a sortable column is one `SortColumn<User>` entry in the `SortMap`. Searchable columns are added to the `SearchableColumns` array. See listBase plan for how `ToKeysetPageAsync` handles new types (already generic via `JsonElement` + `SortColumn.ClrType`).
- **Role sort order.** If product wants a custom order (Admin always first), add a `RoleSortKeyMapping` per listBase plan.
- **Total count truncation.** Only if `COUNT(*)` becomes a measurable cost; don't pre-optimise.
