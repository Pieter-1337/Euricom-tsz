---
name: 'frontend-form'
description: >
  Add a form to an existing route in packages/web. Uses the shared `useAppForm`
  base (TanStack Form + Zod + bound field components), `createServerFn` submission,
  server-error wiring, and `<form.FormActions>` for buttons. Use after
  frontend-feature, or standalone for any form need.
paths: packages/web/**
---

# Frontend Form

Adds a form to an existing route using the shared form base in
`packages/web/src/components/form/form-context.tsx`. The base hides TanStack Form
boilerplate (field wrappers, submit/cancel, server errors). Callers only own:
schema, defaults, fields list, and submit business logic.

## What the base provides

- `useAppForm(...)` — replaces `useForm`. Same API, plus bound components.
- Bound field components (inside `<form.AppField>`): `TextField`, `NumberField`,
  `SelectField`, `TextareaField`, `CheckboxField`, `DateField`. Each renders
  Label + input + `FieldError` and reads/writes the field via context.
- Bound form components (inside `<form.AppForm>`): `FormErrorBanner`,
  `FormActions`, `SubmitButton`, `CancelButton`.
- `useFormServerErrors(form, fieldNames)` — applies ProblemDetails field
  errors via `errorMap.onServer` and exposes the banner message.

Behavior baked in:
- Submit disabled when client errors, submitting, OR no changes from baseline
  (deep-equal `state.values` vs `form.options.defaultValues`).
- Cancel disabled when no changes (when using default reset behavior).
- Manual revert to original values clears the dirty state automatically.
- After a successful save, call `form.reset(value)` to make the saved values the
  new baseline so Save/Cancel go inert until further edits.

## Conventions

- Zod schema drives both client-side `onChange` validation and server-side `inputValidator`.
- Fields rendered via `<form.AppField>` + bound `field.<Type>Field` — never controlled inputs outside this pattern.
- Submit/cancel rendered via `<form.FormActions />` — never hand-rolled `<form.Subscribe>` for buttons unless you need a layout the component can't express.
- Top-level (banner) server error rendered via `<form.FormErrorBanner message={serverError} />`.
- All form-level bound components live inside `<form.AppForm>...</form.AppForm>` (provides form context).
- After a successful mutation: `await router.invalidate()` + `form.reset(value)` for edit forms.
- Import alias `#/` maps to `src/`.

## Cancel button — opt-in

`<form.FormActions />` shows only Save by default. To show Cancel, pass the `cancel` prop:

- `cancel` (or `cancel={true}`) — reset to baseline; disabled when no changes. Use for **edit** forms.
- `cancel={onClose}` — custom action (e.g. close a dialog); always enabled. Use when Cancel means "exit" rather than "revert".
- Omit `cancel` entirely — Save only. Use for **create** forms (no original to revert to).

## Step 1 — Clarify scope

A form is a configuration. The user must explicitly decide every item below;
don't infer fields, validation rules, or button behavior from surrounding code.

**Where the answers come from**, in priority order:
1. An existing plan the agent has been asked to implement — whether a file (`PLAN.md`, a doc under `docs/`, a linked spec) or a plan agreed upon earlier in the conversation. Trust it, do not re-ask. Briefly summarise back what it specifies so the user can correct it.
2. The user's current message — answers given inline.
3. `AskUserQuestion` — for anything still unspecified after 1 and 2.

Only ask the gaps. If the plan covers everything, skip straight to Step 2.

**Items to confirm** (ask only those not already answered):

**Placement**
- Which route file gets the form? (Standalone page, section inside a route, dialog?)

**Mode**
- Create (POST) or edit (PUT/PATCH)?
- For edit: where do the default values come from? (`Route.useLoaderData()` is the default.)
- Which API endpoint does it call? Confirm the request/response shape.

**Fields** (per field)
- Field name (matches API contract).
- Field type — which bound component? (`TextField`, `NumberField`, `SelectField`, `TextareaField`, `CheckboxField`, `DateField`.)
- Label.
- Required?
- For `SelectField`: which options/enum?
- For `NumberField`: min/max, suffix?
- For `TextareaField`: rows?

**Validation**
- Client-side rules per field (min length, email, regex, range, etc.) — these go in the Zod schema.
- Server-side rules to expect (uniqueness, cross-field) — these surface via `useFormServerErrors`.

**Buttons**
- Save label and pending label (e.g. "Create user" / "Creating…").
- Cancel:
  - none — typical for create forms
  - `cancel` — reset to baseline (edit forms)
  - `cancel={onClose}` — close a dialog
  - `cancel={() => router.navigate(...)}` — navigate away
- Any extra actions beyond Save/Cancel (e.g. Delete)? These go **outside** `<form.FormActions />` and usually outside the form section entirely.

**Post-submit behavior**
- After success: navigate where? Invalidate which routes? Reset form to saved values (edit)?
- After failure: banner only, or field-level highlighting?

Only after every item is settled — by plan, by the user's message, or by direct question — should you proceed to Step 2.

## Step 2 — Zod schema

Near the top of the route file:

```ts
import { z } from 'zod';

const <feature>FormSchema = z.object({
  fieldName: z.string().min(1, 'Required'),
  // ...
});

const save<Feature>InputSchema = z.object({
  id: z.string(), // omit for create-only
  <feature>: <feature>FormSchema,
});
```

## Step 3 — Server function

```ts
import { createServerFn } from '@tanstack/react-start';
import { throwApiError } from '#/lib/server-error';
import { update<Feature> } from '#/api/<feature>.server';

const save<Feature> = createServerFn({ method: 'POST' })
  .inputValidator(save<Feature>InputSchema)
  .handler(async ({ data }) => {
    try {
      await update<Feature>(data.id, data.<feature>);
    } catch (e) {
      throwApiError(e);
    }
  });
```

`throwApiError` is mandatory — it re-throws `ApiRequestError` in the serialized
form `parseServerError` (used inside `useFormServerErrors`) can recognise.

## Step 4 — Form component (edit example)

```tsx
import { useRouter } from '@tanstack/react-router';
import { useAppForm } from '#/components/form/form-context';
import { useFormServerErrors } from '#/lib/use-form-server-errors';

function Edit<Feature>() {
  const item = Route.useLoaderData();
  const router = useRouter();

  const form = useAppForm({
    defaultValues: {
      fieldName: item?.fieldName ?? '',
      // match schema fields exactly
    },
    validators: { onChange: <feature>FormSchema },
    onSubmit: async ({ value }) => {
      if (!item) return;
      clearServerErrors();
      try {
        await save<Feature>({ data: { id: item.id, <feature>: value } });
        await router.invalidate();
        form.reset(value); // make saved values the new baseline
      } catch (e) {
        handleApiError(e);
      }
    },
  });

  const { serverError, clearServerErrors, handleApiError } = useFormServerErrors(
    form,
    ['fieldName'], // every field name as a string literal
  );

  return (
    <form.AppForm>
      <form.FormErrorBanner message={serverError} />
      <form
        onSubmit={(e) => { e.preventDefault(); e.stopPropagation(); form.handleSubmit(); }}
        className="grid max-w-md gap-4"
      >
        <form.AppField name="fieldName">
          {(field) => <field.TextField label="Field label" />}
        </form.AppField>

        <form.FormActions cancel />
      </form>
    </form.AppForm>
  );
}
```

## Create-form variant

For a create form (no original entity):

```tsx
const form = useAppForm({
  defaultValues: { name: '', email: '' },
  validators: { onChange: createSchema },
  onSubmit: async ({ value }) => {
    clearServerErrors();
    try {
      const created = await submitCreate({ data: value });
      await router.invalidate();
      if (created) router.navigate({ to: '/.../$id', params: { id: created.id } });
    } catch (e) {
      handleApiError(e);
    }
  },
});

// ...
<form.FormActions saveLabel="Create" savePendingLabel="Creating…" />
// no `cancel` — create forms typically don't offer revert-to-empty
```

## Dialog-form variant

For a form inside a `<Dialog>`:

```tsx
<DialogFooter>
  <form.FormActions
    saveLabel="Update"
    savePendingLabel="Saving…"
    cancel={onClose}
    className="contents"
  />
</DialogFooter>
```

- `cancel={onClose}` — Cancel closes the dialog (always enabled).
- `className="contents"` — the wrapper div is transparent, letting `DialogFooter`'s own flex/spacing layout the buttons.

If the dialog re-mounts on each open (i.e. the form instance lives across leaves), call `form.reset(...)` when the source data changes:

```tsx
const [prevId, setPrevId] = useState<string | null>(null);
const currentId = item?.id ?? null;
if (prevId !== currentId) {
  setPrevId(currentId);
  form.reset({ fieldName: item?.fieldName ?? '' });
}
```

## Field component reference

| Component | Wraps | Value type | Extra props |
|---|---|---|---|
| `TextField` | `Input` | `string` | `type?: string` |
| `NumberField` | `Input type=number` | `number` | `suffix?: string`, `min?: number`; `label` is `ReactNode` |
| `SelectField<T>` | native `<select>` | `T extends string` | `options: readonly T[]` |
| `TextareaField` | `Textarea` | `string` | `rows?: number` |
| `CheckboxField` | `Checkbox` | `boolean` | — |
| `DateField` | `Input type=date` | `string` (ISO `YYYY-MM-DD`) | — |

All field components require a `label` prop and render their own Label + FieldError.

## Server-error handling

`useFormServerErrors(form, fieldNames)`:
- Pass every field name (as string literals) — used to clear `errorMap.onServer` at submit start.
- `clearServerErrors()` resets `onServer` errors on all listed fields.
- `handleApiError(e)`:
  - Parses with `parseServerError` (recognises `throwApiError` payloads).
  - Applies per-field errors from `ProblemDetails.errors` with camelCase key normalisation (first letter lowercase).
  - Sets `serverError` to `apiErr.userMessage` (falls back to `'Something went wrong.'`).

For dialogs without field-level server errors (just a banner), it's fine to manage `serverError` with a local `useState` instead of pulling in `useFormServerErrors`.

## Step 5 — Verify

Run `bun --filter web check` (or `cd packages/web && bun run check`). Check:
- Zod schema fields match `defaultValues` shape exactly.
- Server function `inputValidator` schema matches what `onSubmit` passes as `data`.
- `useFormServerErrors` `fieldNames` lists every form field.
- `router.invalidate()` + `form.reset(value)` are called after successful edit submission.
- Form-level bound components (`FormErrorBanner`, `FormActions`) sit inside `<form.AppForm>`.

For UI changes, also start the dev server and exercise the golden path + at least one server-error path in the browser.
