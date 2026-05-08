# Plan: Cursor-based Pagination for /api/animals

## Context

The codebase is a .NET 9 minimal-API backend (`packages/api`) backed by SQLite/EF Core, paired with a TanStack Start React frontend (`packages/web`). The only module right now is `Animals`. The frontend uses `openapi-fetch` with a generated `schema.ts` to talk to the API.

Current state of the list endpoint:

- **Route**: `GET /api/animals` (defined in `AnimalEndpoints.cs`)
- **Service method**: `AnimalService.GetAllAsync` — calls `_db.Animals.ToListAsync(ct)` with no filtering, ordering, or limits
- **Frontend**: `getAnimals()` in `packages/web/src/api/animals.ts` calls `GET /api/animals` with no parameters; the animals route renders all rows in a table

The dataset is ~100 seed rows today but can grow unboundedly.

---

## Design Decisions

### Why cursor-based, not offset-based?

- **Stable across inserts/deletes**: offset pagination skips or duplicates rows when the underlying data changes between pages. Cursors are immune.
- **Efficient at scale**: `WHERE id > :cursor LIMIT :limit` uses the primary-key index; `OFFSET n` performs a full-table scan up to row _n_.
- **Simple cursor for this model**: `Animal.Id` is an auto-increment integer and is already the natural sort key, making it a perfect opaque cursor.

### Cursor encoding

Encode the cursor as a base64 string of the raw `Id` integer. This keeps the API opaque (clients cannot rely on cursor internals) while remaining trivially decodable server-side.

---

## Step-by-step Plan

### 1. Backend — Contracts (`AnimalContracts.cs`)

Add two new types alongside the existing request records:

```csharp
public class GetAnimalsRequest
{
    [Range(1, 100)]
    public int Limit { get; set; } = 20;

    public string? Cursor { get; set; }  // base64-encoded last-seen Id
}

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public string? NextCursor { get; init; }  // null means no more pages
    public bool HasNextPage => NextCursor is not null;
}
```

`GetAnimalsRequest` is kept as a plain class (not a record) to match the existing style in the file and to support model binding from query parameters.

### 2. Backend — Service (`AnimalService.cs`)

Replace `GetAllAsync` with a paginated overload:

```csharp
public async Task<PagedResult<Animal>> GetPageAsync(
    GetAnimalsRequest request,
    CancellationToken ct = default)
{
    var limit = request.Limit;
    int? afterId = DecodeCursor(request.Cursor);

    var query = _db.Animals.AsNoTracking().OrderBy(a => a.Id);

    if (afterId.HasValue)
        query = query.Where(a => a.Id > afterId.Value).OrderBy(a => a.Id);

    // Fetch one extra to know if a next page exists
    var items = await query.Take(limit + 1).ToListAsync(ct);

    string? nextCursor = null;
    if (items.Count > limit)
    {
        items.RemoveAt(items.Count - 1);
        nextCursor = EncodeCursor(items[^1].Id);
    }

    return new PagedResult<Animal> { Items = items, NextCursor = nextCursor };
}

private static string? EncodeCursor(int id) =>
    Convert.ToBase64String(BitConverter.GetBytes(id));

private static int? DecodeCursor(string? cursor)
{
    if (cursor is null) return null;
    try
    {
        var bytes = Convert.FromBase64String(cursor);
        return bytes.Length == 4 ? BitConverter.ToInt32(bytes) : null;
    }
    catch { return null; }
}
```

Key implementation notes:
- `AsNoTracking()` is appropriate because this is a read-only list query.
- `OrderBy(a => a.Id)` must be explicit and stable — EF Core does not guarantee order without it.
- The +1 trick avoids a second `COUNT` query.

### 3. Backend — Endpoint (`AnimalEndpoints.cs`)

Update the `GET /` handler to bind query parameters and return the paged result:

```csharp
group.MapGet("/", async (
    [AsParameters] GetAnimalsRequest request,
    AnimalService service,
    CancellationToken ct) =>
    TypedResults.Ok(await service.GetPageAsync(request, ct)));
```

`[AsParameters]` tells minimal-API to bind public properties from the query string, which matches the existing pattern for simple types in the project.

### 4. Backend — Validation

The existing `ValidationFilter<T>` uses `DataAnnotations`. Because `GetAnimalsRequest` already carries `[Range(1, 100)]` on `Limit`, add the filter to the GET handler:

```csharp
group.MapGet("/", ...).AddEndpointFilter<ValidationFilter<GetAnimalsRequest>>();
```

### 5. Regenerate the OpenAPI schema

After the backend changes, regenerate `packages/web/src/api/schema.ts` so the frontend's type-safe client picks up the new query parameters and response shape.

The project uses Scalar for the OpenAPI UI (`/openapi/{documentName}.json`). The recommended workflow is:

```bash
# Start the API
bun run dev:api

# Export the spec and regenerate TypeScript types
# (adjust the output path to wherever your openapi-typescript config points)
bunx openapi-typescript http://localhost:5204/openapi/v1.json -o packages/web/src/api/schema.ts
```

The `schema.ts` is auto-generated (noted in the file header); do not edit it manually.

### 6. Frontend — API client (`packages/web/src/api/animals.ts`)

Add a typed pagination function and keep the old `getAnimals` or remove it if no longer needed:

```typescript
export type PagedAnimalsDTO = components['schemas']['PagedResultOfAnimal'];

export const getAnimalPage = async (
  cursor?: string,
  limit = 20
): Promise<PagedAnimalsDTO | undefined> => {
  const resp = await client.GET('/api/animals', {
    params: {
      query: { cursor, limit },
    },
  });
  return resp.data;
};
```

### 7. Frontend — Animals list route (`packages/web/src/routes/animals/index.tsx`)

Convert from a full-load to a cursor-navigable view. The simplest approach that fits the existing TanStack Start pattern is to use a search parameter for the cursor and two navigation buttons.

```tsx
import { z } from 'zod';

const searchSchema = z.object({
  cursor: z.string().optional(),
  limit: z.number().int().min(1).max(100).optional().default(20),
});

export const Route = createFileRoute('/animals/')({
  validateSearch: searchSchema,
  loaderDeps: ({ search }) => search,
  loader: ({ deps }) => fetchAnimals({ data: deps }),
  component: Animals,
});
```

`fetchAnimals` becomes a server function that forwards `cursor` and `limit` to `getAnimalPage`.

In the component, add Prev/Next navigation using shadcn `Button` components (consistent with `$id.tsx`):

```tsx
function Animals() {
  const { items, nextCursor } = Route.useLoaderData();
  const { cursor } = Route.useSearch();
  const navigate = Route.useNavigate();

  return (
    <main>
      <h1 className="text-2xl font-bold">Animals</h1>
      <Table className="mt-4">
        {/* ... existing header and rows ... */}
      </Table>
      <div className="mt-4 flex gap-2">
        <Button
          variant="outline"
          disabled={!cursor}
          onClick={() => navigate({ search: (prev) => ({ ...prev, cursor: undefined }) })}
        >
          First page
        </Button>
        <Button
          variant="outline"
          disabled={!nextCursor}
          onClick={() => navigate({ search: (prev) => ({ ...prev, cursor: nextCursor }) })}
        >
          Next
        </Button>
      </div>
    </main>
  );
}
```

Note: a full "Previous page" button would require storing cursor history (e.g. a stack). For a first iteration, a "Back to first page" button is simpler and already useful. A cursor stack can be added later if needed.

### 8. Update `api.http` for manual testing

Add a sample paginated request:

```http
### Get animals page 1 (default limit 20)
GET {{api_HostAddress}}/api/animals
Accept: application/json

### Get animals page 2 (cursor from previous response)
GET {{api_HostAddress}}/api/animals?cursor=AAAAFAA=&limit=20
Accept: application/json
```

---

## File Change Summary

| File | Change |
|------|--------|
| `packages/api/Modules/Animals/AnimalContracts.cs` | Add `GetAnimalsRequest` and `PagedResult<T>` |
| `packages/api/Modules/Animals/AnimalService.cs` | Replace `GetAllAsync` with `GetPageAsync` + cursor helpers |
| `packages/api/Modules/Animals/AnimalEndpoints.cs` | Update `GET /` to use `[AsParameters] GetAnimalsRequest` and `GetPageAsync` |
| `packages/web/src/api/schema.ts` | Regenerate via `openapi-typescript` (do not edit manually) |
| `packages/web/src/api/animals.ts` | Add `getAnimalPage`; update or remove `getAnimals` |
| `packages/web/src/routes/animals/index.tsx` | Add search param validation, paginated loader, Next/First-page buttons |
| `packages/api/api.http` | Add paginated example requests |

No EF Core migration is needed — no schema change is involved.

---

## Out of Scope (follow-up work)

- **Full "Previous" navigation**: requires a client-side cursor stack (e.g. TanStack Router search state).
- **Sort column / sort direction**: `ORDER BY Id` is hardcoded; generalizing requires careful cursor design per sort key.
- **Filtering by species or name**: can be composed with cursor pagination but is a separate concern.
- **Total count**: cursor-based pagination intentionally avoids `COUNT(*)`. If a total is needed, add a separate `GET /api/animals/count` endpoint.
