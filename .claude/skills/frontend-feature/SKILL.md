---
name: 'frontend-feature'
description: >
  Scaffold a complete new feature in packages/web: feature folder (server fns,
  schemas, components), thin route files, API client functions. Use when adding a
  new page or section. Assumes protected route unless the user specifies public.
paths: packages/web/**
---

# Frontend Feature

Scaffolds a new protected feature end-to-end: API client, feature folder, thin route files.

## Architecture

Routes are **thin composers**. They define the route + loader and render a component imported from the feature folder. Server fns, zod schemas, and components live under `src/features/<name>/`:

```
src/features/<name>/
  schemas.ts        zod schemas + shared types
  server-fns.ts     createServerFn wrappers
  components/
    <name>-list.tsx
    <name>-detail.tsx       (or whatever the feature needs)

src/routes/_protected/<name>/
  index.tsx   (thin — imports the component, defines the route)
  $id.tsx     (thin — loader + thin component that hands data to feature components)
```

Reference implementation: `src/features/users/` + `src/routes/_protected/admin/users/`.

## Conventions

- Import alias `#/` maps to `src/`
- Routes live under `src/routes/_protected/<feature>/`
- Feature code lives under `src/features/<feature>/`
- API client lives in `src/api/<feature>.server.ts`; imports generated types from `./schema`
- Shared code outside features:
  - `src/lib/` — small utils that can run in browser and server (`cn`, `parseServerError`, `authClient`)
  - `src/hooks/` — shared React hooks (`useListQuery`, `useFormServerErrors`)
  - `src/server/` — server-only code (`apiClient`, better-auth instance, auth server fns, `current-user`)
- Never edit `routeTree.gen.ts` — it regenerates on dev start
- Run `bun --filter web gen:api` after backend API contracts change

## Step 1 — Clarify scope

Ask before writing:
- Feature name (kebab-case, used for folder/file names)
- List view, detail view, or both?
- Which API endpoints does it call? Do they exist yet?
- Does it need a create/edit form? If yes, invoke `frontend-form` after scaffolding.

## Step 2 — API client file

`src/api/<feature>.server.ts`:

```typescript
import { apiClient as client } from '#/server/api-client.server';
import type { components } from './schema';

export type <Feature> = components['schemas']['<Feature>'];

export const get<Features> = async (): Promise<<Feature>[]> => {
  const resp = await client.GET('/api/<features>');
  return resp.data ?? [];
};

export const get<Feature>ById = async (id: string): Promise<<Feature> | null> => {
  const resp = await client.GET('/api/<features>/{id}', { params: { path: { id } } });
  return resp.data ?? null;
};
```

The `.server.ts` suffix keeps this file out of the client bundle (it carries the authenticated `apiClient`). Components import server fns instead — never `*.server.ts` directly.

If the schema type is missing, note it — the user runs `bun --filter web gen:api` once the backend is ready.

## Step 3 — Feature folder: schemas + server fns

`src/features/<feature>/schemas.ts`:

```typescript
import { z } from 'zod';

export const <feature>IdSchema = z.string().min(1);
// Add list/sort/form schemas here as the feature grows.
```

`src/features/<feature>/server-fns.ts`:

```typescript
import { createServerFn } from '@tanstack/react-start';
import { get<Features>, get<Feature>ById } from '#/api/<feature>.server';
import { <feature>IdSchema } from '#/features/<feature>/schemas';

export const fetch<Features> = createServerFn({ method: 'GET' }).handler(async () => {
  return await get<Features>();
});

export const fetch<Feature>ById = createServerFn({ method: 'GET' })
  .inputValidator(<feature>IdSchema)
  .handler(async ({ data: id }) => get<Feature>ById(id));
```

Server fns are the safe boundary: components import these (the body is replaced with an RPC stub on the client), never the `.server.ts` API client.

## Step 4 — Feature folder: components

`src/features/<feature>/components/<feature>-list.tsx`:

```typescript
import { Link, useRouter } from '@tanstack/react-router';
import { useQuery } from '@tanstack/react-query';
import { Button } from '#/components/ui/button';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '#/components/ui/table';
import { fetch<Features> } from '#/features/<feature>/server-fns';

export function <Feature>List() {
  const router = useRouter();
  const { data: items = [] } = useQuery({
    queryKey: ['<features>'],
    queryFn: () => fetch<Features>(),
  });

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold"><Feature plural></h1>
        <Button asChild><Link to="/<feature>/new">New <feature></Link></Button>
      </div>
      <Table>
        <TableHeader>
          <TableRow>{/* column headers */}</TableRow>
        </TableHeader>
        <TableBody>
          {items.map((item) => (
            <TableRow
              key={item.id}
              onClick={() => router.navigate({ to: '/<feature>/$id', params: { id: item.id } })}
              className="cursor-pointer"
            >
              {/* cells */}
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
```

For paginated/sortable/searchable lists, use `useListQuery` + `ListShell` + `SortableHeader` (see `src/features/users/components/users-list.tsx`).

`src/features/<feature>/components/<feature>-detail.tsx`:

```typescript
import type { <Feature> } from '#/api/<feature>.server';

export function <Feature>Detail({ item }: { item: <Feature> }) {
  return (
    <div>
      <h1 className="text-2xl font-bold">{item.name ?? `<Feature> ${item.id}`}</h1>
      {/* detail fields */}
    </div>
  );
}
```

`useLoaderData` is route-scoped — only call it inside the route component. Feature components receive data as props.

## Step 5 — Routes (thin composers)

`src/routes/_protected/<feature>/index.tsx`:

```typescript
import { createFileRoute } from '@tanstack/react-router';
import { <Feature>List } from '#/features/<feature>/components/<feature>-list';

export const Route = createFileRoute('/_protected/<feature>/')({
  component: <Feature>List,
});
```

`src/routes/_protected/<feature>/$id.tsx` (when needed):

```typescript
import { createFileRoute } from '@tanstack/react-router';
import { fetch<Feature>ById } from '#/features/<feature>/server-fns';
import { <Feature>Detail } from '#/features/<feature>/components/<feature>-detail';

export const Route = createFileRoute('/_protected/<feature>/$id')({
  loader: ({ params }) => fetch<Feature>ById({ data: params.id }),
  component: <Feature>Page,
});

function <Feature>Page() {
  const item = Route.useLoaderData();
  if (!item) {
    return <main><h1 className="text-2xl font-bold"><Feature> not found</h1></main>;
  }
  return <main><<Feature>Detail item={item} /></main>;
}
```

The route component owns: loader, `useLoaderData()`, the not-found branch. Everything else delegates to feature components.

## Step 6 — Navigation

Add a link in the nav/sidebar. Ask the user where navigation lives if unsure.

## Step 7 — Verify

Run `bun --filter web-tanstack-start check` (covers typecheck + lint). The route appears in `routeTree.gen.ts` automatically on next dev start. If the feature needs a form, invoke the `frontend-form` skill next.
