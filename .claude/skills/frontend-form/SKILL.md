---
name: 'frontend-form'
description: >
  Add a form to an existing route in packages/web. Covers TanStack Form + Zod schema,
  createServerFn POST/PUT submission, field-level error display, and router invalidation.
  Use after frontend-feature, or standalone for any form need.
---

# Frontend Form

Adds a TanStack Form + Zod form to an existing route, wired to a `createServerFn` mutation.

## Conventions
- Zod schema drives both client-side `onChange` validation and server-side `inputValidator`
- Fields rendered via `form.Field` render prop — never controlled inputs outside this pattern
- Submit state via `form.Subscribe` — no separate `useState` for loading/disabled
- Always call `router.invalidate()` after a successful mutation
- Import alias `#/` maps to `src/`

## Step 1 — Clarify scope

Ask before writing:
- Which route file gets the form?
- What fields, and what are their types?
- Create (POST) or edit (PUT/PATCH)?
- For edit: where do the default values come from? (`Route.useLoaderData()` is the default)
- Which API endpoint does it call?

## Step 2 — Zod schema

Add near the top of the route file:

```typescript
import { z } from 'zod';

const <feature>FormSchema = z.object({
  fieldName: z.string().min(1, 'Required'),
  // ...
});

const save<Feature>InputSchema = z.object({
  id: z.number(), // omit for create-only
  <feature>: <feature>FormSchema,
});
```

## Step 3 — Server function

```typescript
import { createServerFn } from '@tanstack/react-start';
import { update<Feature> } from '#/api/<feature>';

const save<Feature> = createServerFn({ method: 'POST' })
  .inputValidator(save<Feature>InputSchema)
  .handler(async ({ data }) => {
    await update<Feature>(data.id, data.<feature>);
  });
```

## Step 4 — Form component

```typescript
import { useForm } from '@tanstack/react-form';
import { useRouter } from '@tanstack/react-router';
import { Input } from '#/components/ui/input';
import { Label } from '#/components/ui/label';
import { Button } from '#/components/ui/button';

function <Feature>Form() {
  const item = Route.useLoaderData();
  const router = useRouter();

  const form = useForm({
    defaultValues: {
      fieldName: item.fieldName ?? '',
      // match schema fields exactly
    },
    validators: { onChange: <feature>FormSchema },
    onSubmit: async ({ value }) => {
      await save<Feature>({ data: { id: item.id, <feature>: value } });
      await router.invalidate();
    },
  });

  return (
    <form onSubmit={(e) => { e.preventDefault(); form.handleSubmit(); }} className="space-y-4">
      <form.Field name="fieldName">
        {(field) => (
          <div>
            <Label htmlFor={field.name}>Field Label</Label>
            <Input
              id={field.name}
              value={field.state.value}
              onChange={(e) => field.handleChange(e.target.value)}
            />
            {field.state.meta.errors.map((err) => (
              <p key={err?.toString()} className="text-sm text-destructive">{err}</p>
            ))}
          </div>
        )}
      </form.Field>

      <form.Subscribe selector={(state) => [state.canSubmit, state.isSubmitting]}>
        {([canSubmit, isSubmitting]) => (
          <Button type="submit" disabled={!canSubmit || isSubmitting}>
            {isSubmitting ? 'Saving...' : 'Save'}
          </Button>
        )}
      </form.Subscribe>
    </form>
  );
}
```

## Step 5 — Verify

Run `bun --filter web typecheck`. Check:
- Zod schema fields match `defaultValues` shape exactly
- Server function `inputValidator` schema matches what `onSubmit` passes as `data`
- `router.invalidate()` is called after successful submission
