# Plan: Cursor-based pagination for GET /api/animals

## Goal

Add cursor-based pagination to the `GET /api/animals` endpoint so the API returns a manageable page of results plus a cursor token the client can use to fetch the next page, replacing the current full-table scan.

## Context

The API is a .NET 9 Minimal API backed by EF Core with a SQLite database (`animals.db`). The `Animal` entity has an integer auto-increment primary key `Id`. The `GET /api/animals` endpoint lives in `AnimalEndpoints.cs` and delegates to `AnimalService.GetAllAsync()`, which currently calls `_db.Animals.ToListAsync(ct)` with no filtering or ordering. Response contracts are in `AnimalContracts.cs`; there is currently no response envelope — the endpoint returns `Animal[]` directly.

The frontend (`packages/web`) uses `openapi-fetch` with a code-generated `schema.ts`. After any API contract change the schema must be regenerated with `bun --filter web gen:api` (calls `openapi-typescript` against the live Scalar/OpenAPI endpoint). Unit tests use in-memory EF Core; integration tests use `WebApplicationFactory`.

For cursor-based pagination on an integer PK the cursor is simply the last `Id` seen. The next page is `WHERE Id > cursor ORDER BY Id ASC LIMIT pageSize`. This avoids offset drift and is O(log n) on the indexed PK.

## Files

- `packages/api/Modules/Animals/AnimalContracts.cs` — modify: add `PagedAnimalsResponse` record (items + nextCursor)
- `packages/api/Modules/Animals/AnimalService.cs` — modify: add `GetPagedAsync(int? afterId, int pageSize, CancellationToken)` method; keep `GetAllAsync` to avoid breaking other callers
- `packages/api/Modules/Animals/AnimalEndpoints.cs` — modify: update `GET /` handler to accept `afterId` and `pageSize` query parameters and return the paged response
- `packages/web/src/api/schema.ts` — regenerate (do not edit manually): run `bun --filter web gen:api` after the API is updated and running
- `packages/web/src/api/animals.ts` — modify: update `getAnimals` to accept optional `afterId` and `pageSize` parameters; return the new `PagedAnimalsResponse` type
- `packages/web/src/routes/animals/index.tsx` — modify: update loader and component to use paged data and render a "Load more" button
- `packages/api.tests/AnimalServiceTests.cs` — modify: add tests for `GetPagedAsync`
- `packages/api.tests.integration/AnimalEndpointsTests.cs` — modify: add integration tests for paginated `GET /api/animals`

## Steps

1. **Add `PagedAnimalsResponse` to `AnimalContracts.cs`.**
   Add a new record after the existing request classes:

   ```csharp
   public record PagedAnimalsResponse(List<Animal> Items, int? NextCursor);
   ```

2. **Add `GetPagedAsync` to `AnimalService.cs`.**
   Insert after `GetAllAsync`:

   ```csharp
   public Task<PagedAnimalsResponse> GetPagedAsync(int? afterId, int pageSize, CancellationToken ct = default)
   {
       var query = _db.Animals.AsQueryable();
       if (afterId.HasValue)
           query = query.Where(a => a.Id > afterId.Value);
       // wrap in an async method so we can build the cursor after the DB call
       return FetchPageAsync(query, pageSize, ct);
   }

   private static async Task<PagedAnimalsResponse> FetchPageAsync(
       IQueryable<Animal> query, int pageSize, CancellationToken ct)
   {
       var items = await query
           .OrderBy(a => a.Id)
           .Take(pageSize + 1)        // fetch one extra to detect if there is a next page
           .ToListAsync(ct);

       int? nextCursor = null;
       if (items.Count > pageSize)
       {
           items.RemoveAt(items.Count - 1);
           nextCursor = items[^1].Id;
       }
       return new PagedAnimalsResponse(items, nextCursor);
   }
   ```

3. **Update the `GET /` handler in `AnimalEndpoints.cs`.**
   Replace:

   ```csharp
   group.MapGet("/", async (AnimalService service, CancellationToken ct) =>
       TypedResults.Ok(await service.GetAllAsync(ct)));
   ```

   With:

   ```csharp
   group.MapGet("/", async (
       [FromQuery] int? afterId,
       [FromQuery] int pageSize,
       AnimalService service,
       CancellationToken ct) =>
   {
       const int defaultPageSize = 20;
       const int maxPageSize = 100;
       var size = pageSize > 0 ? Math.Min(pageSize, maxPageSize) : defaultPageSize;
       return TypedResults.Ok(await service.GetPagedAsync(afterId, size, ct));
   });
   ```

   Add `using Microsoft.AspNetCore.Mvc;` at the top if not already present (needed for `[FromQuery]`).

4. **Regenerate the OpenAPI TypeScript schema.**
   Start the API (`bun dev:api`) then run:

   ```
   bun --filter web gen:api
   ```

   This overwrites `packages/web/src/api/schema.ts` with the updated spec including `PagedAnimalsResponse`.

5. **Update `packages/web/src/api/animals.ts`.**
   - Export a new type alias: `export type PagedAnimalsResponseDTO = components['schemas']['PagedAnimalsResponse'];`
   - Change the signature and implementation of `getAnimals`:
     ```ts
     export const getAnimals = async (
       afterId?: number,
       pageSize?: number,
     ): Promise<PagedAnimalsResponseDTO | undefined> => {
       const resp = await client.GET('/api/animals', {
         params: { query: { afterId, pageSize } },
       });
       return resp.data;
     };
     ```

6. **Update `packages/web/src/routes/animals/index.tsx`.**
   - Change the `fetchAnimals` server function to pass through `afterId` so it can be called for subsequent pages.
   - In the `Animals` component, hold cursor state (`useState<number | undefined>`) and an accumulated list of animals (`useState<AnimalDTO[]>`).
   - On initial load (from `Route.useLoaderData()`), seed the list with the first page's `items` and the `nextCursor`.
   - Render a "Load more" button that is only visible when `nextCursor` is defined; clicking it calls `fetchAnimals({ data: { afterId: nextCursor } })`, appends the new items, and updates the cursor.

7. **Update `packages/api/api.http`.**
   Add example requests for the paginated endpoint:

   ```
   ### Get first page of animals (20 per page)
   GET {{api_HostAddress}}/api/animals?pageSize=20
   Accept: application/json

   ### Get next page using cursor
   GET {{api_HostAddress}}/api/animals?afterId=20&pageSize=20
   Accept: application/json
   ```

## Tests

### Unit tests — `packages/api.tests/AnimalServiceTests.cs`

- `GetPaged_NoAfterCursor_ReturnsFirstPage`: seed 25 animals, call `GetPagedAsync(null, 20)`, assert 20 items returned and `NextCursor` equals the 20th animal's Id.
- `GetPaged_WithAfterCursor_ReturnsNextPage`: seed 25 animals, call `GetPagedAsync(cursor, 20)`, assert remaining items returned and `NextCursor` is null.
- `GetPaged_LastPage_ReturnsNullCursor`: seed 5 animals, call `GetPagedAsync(null, 20)`, assert all 5 items returned and `NextCursor` is null.
- `GetPaged_EmptyTable_ReturnsEmptyListAndNullCursor`: call on empty DB, assert empty items and null cursor.
- `GetPaged_PageSizeOne_ReturnsSingleItem`: seed 3 animals, call with `pageSize=1`, assert 1 item and a non-null cursor.

### Integration tests — `packages/api.tests.integration/AnimalEndpointsTests.cs`

- `GetAnimals_Paginated_ReturnsOkWithPagedResponse`: GET `/api/animals?pageSize=5`, assert 200, `items.Count == 5`, `nextCursor` is not null.
- `GetAnimals_Paginated_SecondPage_UsesCursor`: GET first page, then GET `/api/animals?afterId={nextCursor}&pageSize=5`, assert the first item's Id is greater than the cursor.
- `GetAnimals_Paginated_DefaultPageSize_Returns20Items`: seed 25 animals, GET `/api/animals`, assert 20 items.
- `GetAnimals_Paginated_PageSizeExceedsMax_ClampsTo100`: GET `/api/animals?pageSize=999`, assert at most 100 items.
- `GetAnimals_Paginated_InvalidAfterCursor_ReturnsEmpty`: GET `/api/animals?afterId=99999`, assert `items` is empty and `nextCursor` is null.

## Edge Cases

- **`pageSize` of 0 or negative**: clamp to default (20) in the endpoint handler.
- **`pageSize` larger than max**: clamp to 100 to prevent DoS.
- **`afterId` referencing a deleted animal**: the `WHERE Id > afterId` query skips the gap gracefully; no error, no missed rows.
- **Concurrent inserts between pages**: new records inserted between two page fetches will appear on subsequent pages (cursor is stable), but records inserted before the cursor will not appear. This is acceptable for a cursor design.
- **`afterId` pointing exactly to the last record**: returns empty `items` and `null` cursor correctly.
- **OpenAPI schema regeneration**: `schema.ts` is auto-generated; never edit it manually. If `PagedAnimalsResponse` does not appear in the generated schema, ensure the API is running and that the response type is correctly inferred by the .NET OpenAPI source generator (may need an explicit `Produces<PagedAnimalsResponse>()` call on the endpoint).

## Assumptions

- The existing `GetAllAsync` method is kept intact; nothing else in the codebase calls it from outside the module, but removing it now would be an unnecessary breaking change.
- Default page size of 20 and max of 100 are reasonable defaults; adjust if the product owner specifies otherwise.
- The frontend "Load more" pattern is preferred over full server-side pagination controls (no total-count or page-number UI needed at this stage).
- The `[FromQuery]` attribute is available without extra packages; `Microsoft.AspNetCore.Mvc` is already transitively referenced in Minimal API projects.
- Running `bun --filter web gen:api` requires the API to be running locally on port 5204 (as configured in `packages/web/package.json`).
