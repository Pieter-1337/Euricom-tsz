---
name: 'frontend-feature'
description: >
  Scaffold a complete new feature in packages/web: protected route file(s), loader via
  createServerFn, API client functions, and basic UI shell. Use when adding a new page
  or section. Assumes protected route unless the user specifies public.
paths: packages/web/**
---

# Frontend Feature

Scaffolds a new protected feature end-to-end: route, loader, API client, UI shell.

## Conventions
- Import alias `#/` maps to `src/`
- Routes live under `src/routes/_protected/<feature>/`
- API client lives in `src/api/<feature>.ts`; imports generated types from `./schema`
- Never edit `routeTree.gen.ts` — it regenerates on dev start
- Run `bun --filter web gen:api` after backend API contracts change

## Step 1 — Clarify scope

Ask before writing:
- Feature name (kebab-case, used for folder/file names)
- List view, detail view, or both?
- Which API endpoints does it call? Do they exist yet?
- Does it need a create/edit form? If yes, invoke `frontend-form` after scaffolding.

## Step 2 — Create the API client file

`src/api/<feature>.ts`:

```typescript
import { type components } from './schema';
import { apiClient as client } from '#/lib/api.server';

export type <Feature>DTO = components['schemas']['<Feature>'];

export const get<Features> = async (): Promise<<Feature>DTO[]> => {
  const resp = await client.GET('/api/<features>');
  return resp.data!;
};

export const get<Feature>ById = async (id: number): Promise<<Feature>DTO> => {
  const resp = await client.GET('/api/<features>/{id}', { params: { path: { id } } });
  return resp.data!;
};
```

Only include functions for endpoints that actually exist. If the schema type is missing, note it — the user runs `bun --filter web gen:api` once the backend is ready.

## Step 3 — List route

`src/routes/_protected/<feature>/index.tsx`:

```typescript
import { createFileRoute, Link } from '@tanstack/react-router';
import { createServerFn } from '@tanstack/react-start';
import { get<Features> } from '#/api/<feature>';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '#/components/ui/table';
import { Button } from '#/components/ui/button';

const fetch<Features> = createServerFn({ method: 'GET' }).handler(async () => {
  return await get<Features>() ?? [];
});

export const Route = createFileRoute('/_protected/<feature>/')({
  loader: () => fetch<Features>(),
  component: <Feature>List,
});

function <Feature>List() {
  const items = Route.useLoaderData();
  return (
    <div className="p-6">
      <h1 className="text-2xl font-bold mb-4"><Feature plural></h1>
      <Table>
        <TableHeader>
          <TableRow>{/* column headers */}</TableRow>
        </TableHeader>
        <TableBody>
          {items.map((item) => (
            <TableRow key={item.id}>
              {/* cells */}
              <TableCell>
                <Button variant="ghost" asChild>
                  <Link to="/_protected/<feature>/$id" params={{ id: String(item.id) }}>View</Link>
                </Button>
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
```

## Step 4 — Detail route (when needed)

`src/routes/_protected/<feature>/$id.tsx`:

```typescript
import { createFileRoute } from '@tanstack/react-router';
import { createServerFn } from '@tanstack/react-start';
import { z } from 'zod';
import { get<Feature>ById } from '#/api/<feature>';

const idSchema = z.object({ id: z.coerce.number() });

const fetch<Feature>ById = createServerFn({ method: 'GET' })
  .inputValidator(idSchema)
  .handler(async ({ data: id }) => get<Feature>ById(id));

export const Route = createFileRoute('/_protected/<feature>/$id')({
  loader: ({ params }) => fetch<Feature>ById({ data: { id: Number(params.id) } }),
  component: <Feature>Detail,
});

function <Feature>Detail() {
  const item = Route.useLoaderData();
  return (
    <div className="p-6">
      <h1 className="text-2xl font-bold mb-4">{item.name ?? `<Feature> ${item.id}`}</h1>
      {/* detail fields */}
    </div>
  );
}
```

## Step 5 — Navigation

Add a link in the nav/sidebar. Ask the user where navigation lives if unsure.

## Step 6 — Verify

Run `bun --filter web typecheck`. The route appears in `routeTree.gen.ts` automatically on next dev start. If the feature needs a form, invoke the `frontend-form` skill next.
