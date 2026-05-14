# Plan: User list — server-side search, sort, active toggle, total count, keyset pagination, infinite scroll

Consumes the listBase primitives defined in [../generics/list/plan.md](../generics/list/plan.md).

## Goal

Build a server-driven admin user list matching `admin-user-list.png` using keyset pagination and infinite scroll. Keep the existing plain `GET /api/users` endpoint unchanged for dropdown/lookup consumers; add a new `GET /api/users/paged` endpoint for the rich admin overview:

- Free-text filter (case-insensitive contains over FirstName + LastName + Email).
- Active toggle: default ON = exclude soft-deleted, OFF = include them. No new model field — bypass the EF `HasQueryFilter` when OFF.
- Total user count label ("X users").
- Sortable columns (Name, Email, Role) — single-column, asc/desc toggle.
- Keyset pagination with infinite scroll (see listBase plan).

## Non-goals

- New `User` fields (no Personnel Number, First/Last/Nickname, etc.). FirstName, LastName, Email, Role are the only sortable/searchable columns. Leaves plan is separate.
- Full-text search. The generic `LOWER(col) LIKE '%term%'` is sufficient.

## API contract

### Plain endpoint (unchanged)

`GET /api/users` → `IEnumerable<UserDto>`. No query params. Returns all non-soft-deleted users via the standard query filter. Use case: dropdown population, role-filtered lookups, or any consumer that wants a flat list without search/sort/pagination. Admin-only (`RequireAdmin`). Status codes: `200 OK`, `403` for non-admins.

### Paged endpoint (new)

`GET /api/users/paged` — generic shape per the listBase plan. User-specific details:

- **Query params**: `search`, `sortBy`, `sortDir`, `pageSize`, `cursor`, `includeDeleted` (see listBase plan for details).
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

### Plain endpoint (unchanged) — `Modules/Users/Features/GetUsers.cs`

```csharp
public sealed record GetUsersQuery : IQuery<IReadOnlyList<UserDto>>;

public sealed class GetUsersHandler(IUnitOfWork uow)
    : IQueryHandler<GetUsersQuery, IReadOnlyList<UserDto>>
{
    public async Task<IReadOnlyList<UserDto>> HandleAsync(GetUsersQuery query, CancellationToken ct = default)
    {
        var users = await uow.RepositoryFor<User>().GetAllAsDtosAsync<UserDto>(ct: ct);
        return users.ToList();
    }
}
```

Endpoint: `adminGroup.MapGet("/", ...)` in `UserEndpoints.cs` — no query params, returns all non-soft-deleted users.

### Paged endpoint (new) — `Modules/Users/Features/GetUsersPaged.cs`

```csharp
public sealed record GetUsersPagedQuery(
    string? Search,
    string? SortBy,
    SortDirection SortDir,
    int PageSize,
    string? Cursor,
    bool IncludeDeleted)
    : KeysetQueryOptions(Search, SortBy, SortDir, PageSize, Cursor, IncludeDeleted);

public sealed class GetUsersPagedQueryValidator : KeysetQueryOptionsValidator<GetUsersPagedQuery, User>
{
    public GetUsersPagedQueryValidator(SortMap<User> sortMap) : base(sortMap)
    {
        // Add user-specific rules beyond the generic base if needed.
        // For now, no extras.
    }
}

public sealed class GetUsersPagedHandler(IUnitOfWork uow)
    : IQueryHandler<GetUsersPagedQuery, KeysetPage<UserDto>>
{
    private static readonly SortMap<User> Sort = new(
        new SortColumn<User>("name", u => u.FirstName + " " + u.LastName, typeof(string)),
        new SortColumn<User>("email", u => u.Email, typeof(string)),
        new SortColumn<User>("role", u => u.Role, typeof(string)));

    private static readonly Expression<Func<User, string>>[] Searchable = new[]
    {
        (Expression<Func<User, string>>)(u => u.FirstName),
        (Expression<Func<User, string>>)(u => u.LastName),
        (Expression<Func<User, string>>)(u => u.Email),
    };

    public async Task<KeysetPage<UserDto>> HandleAsync(GetUsersPagedQuery query, CancellationToken ct = default)
    {
        return await uow.RepositoryFor<User>().GetPagedAsync(
            query, Sort, Searchable, UserDto.Project,
            ct: ct, ignoreQueryFilters: query.IncludeDeleted);
    }
}
```

**Note**: `IRepository<TEntity>.GetPagedAsync<TDto>` is a thin facade over `KeysetQueryableExtensions.ToKeysetPageAsync`. It handles the `baseQuery` filtering + soft-delete toggle internally; the handler just passes the sort map and searchable columns. This keeps handlers decoupled from the keyset mechanics.

**SortMap choice**: `name` maps to `FirstName + " " + LastName` (concatenated for sort purposes). Ties break by `Id`. Using `LastName` as a secondary sort is deferred; the current UI does not require it.

### Endpoint mapping — `Modules/Users/UserEndpoints.cs`

```csharp
// Plain endpoint (unchanged)
adminGroup.MapGet("/", async (IDispatcher dispatcher, CancellationToken ct) =>
    TypedResults.Ok(await dispatcher.SendAsync(new GetUsersQuery(), ct)));

// Paged endpoint (new)
adminGroup.MapGet("/paged", async ([AsParameters] GetUsersPagedQuery query, IDispatcher dispatcher, CancellationToken ct) =>
    TypedResults.Ok(await dispatcher.SendAsync(query, ct)))
    .WithName("GetUsersPaged")
    .WithOpenApi();
```

## Frontend implementation

### `src/api/users.ts`

Plain endpoint (kept for future consumers):

```ts
export async function getUsers(): Promise<UserDto[]> {
  const res = await fetch('/api/users');
  if (!res.ok) throw new Error(`Failed to fetch users: ${res.status}`);
  return res.json();
}
```

Paged endpoint (new, for admin list):

```ts
import { KeysetQueryParams, KeysetPage } from './pagination';
import { UserDto } from './schema';

export type ListUsersParams = KeysetQueryParams<'name' | 'email' | 'role'>;

export async function getUsersPaged(params: ListUsersParams): Promise<KeysetPage<UserDto>> {
  const query = new URLSearchParams(
    Object.entries(params).filter(([, v]) => v !== undefined) as [string, string][]
  );
  const res = await fetch(`/api/users/paged?${query}`);
  if (!res.ok) throw new Error(`Failed to fetch users: ${res.status}`);
  return res.json();
}
```

### `src/routes/_protected/admin/users/index.tsx`

```tsx
import { useListQuery } from '@/lib/use-list-query';
import { ListToolbar } from '@/components/list/list-toolbar';
import { InfiniteTable } from '@/components/list/infinite-table';
import { SortableHeader } from '@/components/list/sortable-header';
import { getUsersPaged } from '@/api/users';
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
    queryKey: ['users-paged'],
    fetcher: getUsersPaged,
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

`GetUsersPagedQueryValidator` inherits all generic rules from `KeysetQueryOptionsValidator<GetUsersPagedQuery, User>` (see listBase plan):

- `Search`: max 200 chars if set.
- `SortBy`: must be in `{name, email, role}` if set.
- `SortDir`: valid enum value if set.
- `PageSize`: 1–200 if set.
- `Cursor`: decodes successfully if set.

No user-specific validator extras needed for v1.

## Tests

### Unit tests (`Tsz.Api.Tests`)

Existing plain endpoint (unchanged):

- `GetUsersHandlerTests` — all existing cases pass; handler returns flat `IReadOnlyList<UserDto>`.

New paged endpoint:

- `GetUsersPagedHandlerTests`:
  - Empty DB → `Items: [], NextCursor: null, Total: 0`.
  - 50 users, default sort (name asc) → first page returns 50, `NextCursor` null, `Total: 50`.
  - 51 users, `pageSize=50` → first page 50 + non-null cursor; second page 1 + null cursor.
  - Search "john" → matches FirstName, LastName, and Email case-insensitively.
  - `SortBy=email, SortDir=desc` → reversed by email, cursor still works.
  - Soft-deleted rows: hidden by default, visible when `IncludeDeleted=true`, `Total` reflects the toggle.
  - Tiebreaker: two users with identical names → deterministic order by `Id`.
- `GetUsersPagedQueryValidatorTests` — inherits generic rules from base class.

### Integration tests (`Tsz.Api.Tests.Integration`)

Existing plain endpoint (unchanged):

- `UserEndpointsTests` — existing `GET /api/users` cases remain green; no changes.

New paged endpoint:

- `UserEndpointsTests` — new cases for `GET /api/users/paged`:
  - Non-admin → 403 on `GET /api/users/paged`.
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

- **Plain endpoint footgun.** `GET /api/users` always filters out soft-deleted rows (standard query filter). To include deleted users, callers must use `GET /api/users/paged?includeDeleted=true`. No way to bypass the query filter on the plain endpoint by design — it's a simple lookup endpoint.
- **Role sort order.** Enum values sort lexicographically: `Admin < ClientManager < User`. Document in tooltips if needed; no custom mapping for v1.
- **FirstName + LastName "Name" sort.** Concatenates as `FirstName + " " + LastName` for the cursor's sort value. Ties break by `Id` (see listBase plan).

All other edge cases (concurrent inserts, partial pages, page-size churn, URL persistence) are handled by the generic listBase primitives or explicitly deferred.

## Steps

Assumes listBase primitives are already in place (see [../generics/list/plan.md](../generics/list/plan.md)).

1. **API** — Add `GetPagedAsync<TDto>` extension method to `IRepository<TEntity>` (thin facade over `KeysetQueryableExtensions.ToKeysetPageAsync`). Wire it in the EF implementation.
2. **API** — Create `Features/GetUsersPaged.cs` with `GetUsersPagedQuery` (subclass `KeysetQueryOptions`), `GetUsersPagedQueryValidator` (subclass `KeysetQueryOptionsValidator<GetUsersPagedQuery, User>`), and `GetUsersPagedHandler` (calls `repo.GetPagedAsync(...)`).
3. **API** — Add `MapGet("/paged", ...)` to `UserEndpoints.cs`; leave existing `MapGet("/", ...)` unchanged.
4. **API** — Unit tests for `GetUsersPagedHandlerTests` and `GetUsersPagedQueryValidatorTests`. Integration tests for `GET /api/users/paged`.
5. **Regen** — `bun --filter web gen:api` to sync schema types.
6. **Web** — Add `getUsersPaged` function to `src/api/users.ts`; keep `getUsers` for future use.
7. **Web** — Rewrite `_protected/admin/users/index.tsx` to use `useListQuery({ fetcher: getUsersPaged, ... })`, `<ListToolbar>`, `<InfiniteTable>`, `<SortableHeader>`.
8. **Smoke test** the manual checklist.

## Open questions deferred

- **New User fields.** Adding a sortable column is one `SortColumn<User>` entry in the `SortMap`. Searchable columns are added to the `Searchable` array. See listBase plan for how `ToKeysetPageAsync` handles new types (already generic via `JsonElement` + `SortColumn.ClrType`).
- **Role sort order.** If product wants a custom order (Admin always first), add a `RoleSortKeyMapping` per listBase plan.
- **Total count truncation.** Only if `COUNT(*)` becomes a measurable cost; don't pre-optimise.
- **Plain endpoint use cases.** Future consumers (e.g. dropdown in UserLeave or UserRoles form) should use `GET /api/users`. If a consumer needs search/sort/pagination, add a new `GET /api/xyz/paged` endpoint per the same pattern.
