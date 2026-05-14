# Plan: Form scaffolding (`formBase`) for packages/web

## Goal

Extract TanStack Form boilerplate (field wrappers, submit button, server-error handling) into reusable primitives so each route owns only its schema, defaults, fields list, and business logic. Use TanStack Form's `createFormHook` API to mint a `useAppForm` with bound field components — the React equivalent of Angular Material's `formControlName`.

## Context

- TanStack Form `^1.29.1` in use; `createFormHook`/`useAppForm` is the v1.x-native pattern for shared bound field components.
- Three forms currently exist, all duplicating the same scaffolding:
  - `packages/web/src/routes/_protected/admin/users/new.tsx`
  - `packages/web/src/routes/_protected/admin/users/$id.tsx` (general section)
  - `EditLeaveDialog` inside `$id.tsx`
- Duplicated bits: `FieldError` component (verbatim copies), `<form onSubmit>` boilerplate, server-error→`setFieldMeta` dance with camelCase key conversion, `<form.Subscribe>` submit button, native `<select>` block with long Tailwind string, the `serverError` banner `<p>`.
- Existing helpers we keep: `hasClientSideError` in `lib/form-utils.ts`, `parseServerError`/`throwApiError` in `lib/server-error.ts`.
- shadcn primitives currently installed: `input`, `label`, `button`, `table`, `dialog`, `select`. We will add `textarea` and `checkbox`.

## Files

**New:**
- `packages/web/src/components/form/form-context.tsx` — `createFormHook` setup; exports `useAppForm`, `withForm`, and bound components: `TextField`, `NumberField`, `SelectField`, `TextareaField`, `CheckboxField`, `DateField`, `SubmitButton`, `FormErrorBanner`.
- `packages/web/src/components/form/field-error.tsx` — single shared `FieldError` (replaces both inline copies).
- `packages/web/src/components/ui/textarea.tsx` — shadcn primitive (via `bun x shadcn@latest add textarea`).
- `packages/web/src/components/ui/checkbox.tsx` — shadcn primitive (via `bun x shadcn@latest add checkbox`).
- `packages/web/src/lib/use-form-server-errors.ts` — hook returning `{ serverError, handleApiError, clearServerErrors }`.

**Modify:**
- `packages/web/src/routes/_protected/admin/users/new.tsx` — replace inline `<form.Field>` + Input/Label/FieldError blocks with `<form.AppField>` + `<field.TextField label="…" />`; drop local `FieldError`; use `useFormServerErrors`.
- `packages/web/src/routes/_protected/admin/users/$id.tsx` — same conversion for both forms (general + `EditLeaveDialog`).

**Keep:**
- `packages/web/src/lib/form-utils.ts` — used internally by `SubmitButton`; no public callers after refactor.

## Steps

1. Install shadcn primitives: `bun x shadcn@latest add textarea checkbox` (run from `packages/web`).
2. Create `components/form/field-error.tsx` — extract the `FieldError` component currently duplicated in `new.tsx` and `$id.tsx` verbatim.
3. Create `components/form/form-context.tsx`:
   - `createFormHookContexts()` → `fieldContext`, `formContext`, `useFieldContext`, `useFormContext`.
   - Bound field components (each ~15 lines):
     - `TextField({ label, type? })` — Label + Input + FieldError; sets `aria-invalid` from field meta.
     - `NumberField({ label, suffix?, min? })` — coerces via `Number()` on change.
     - `SelectField<T>({ label, options })` — native `<select>` for now; generic over option value type so `UserRole` round-trips without casts.
     - `TextareaField({ label, rows? })` — wraps shadcn `Textarea`.
     - `CheckboxField({ label })` — wraps shadcn `Checkbox`; boolean field. Coerce `indeterminate → false`.
     - `DateField({ label })` — `<Input type="date">`; stores ISO `YYYY-MM-DD` string.
   - Bound form components:
     - `SubmitButton({ label, pendingLabel })` — wraps `form.Subscribe`, disables on `hasClientSideError(state.fieldMeta) || state.isSubmitting`.
     - `FormErrorBanner({ message })` — null-guards, renders the destructive `<p>`.
   - `createFormHook({ fieldComponents, formComponents, fieldContext, formContext })` → `useAppForm`, `withForm`.
4. Create `lib/use-form-server-errors.ts`:
   - `useFormServerErrors<TForm>(form, fieldNames: readonly string[])`.
   - Returns `{ serverError, clearServerErrors(), handleApiError(e) }`.
   - `clearServerErrors` resets `errorMap.onServer = undefined` for every field in `fieldNames`.
   - `handleApiError` parses with `parseServerError`, applies field errors with camelCase keys (first-letter lowercase, matches existing behaviour), sets banner message via `apiErr.userMessage`; fallback `'Something went wrong.'`.
5. Migrate `users/new.tsx`:
   - Swap `useForm` → `useAppForm`.
   - Replace each `<form.Field>` block with `<form.AppField>` + bound field component.
   - Replace inline submit subscribe with `<form.SubmitButton label="Create user" pendingLabel="Creating…" />`.
   - Replace `serverError` `<p>` with `<form.FormErrorBanner message={serverError} />`.
   - Drop local `FieldError`.
   - Verify in browser: create user (golden path), trigger client validation error, trigger duplicate-email server error.
6. Migrate `users/$id.tsx` general form — same swap. Verify: rename, role change, force server field error.
7. Migrate `EditLeaveDialog` in `$id.tsx` — same swap. Keep `form.reset` on prop change (depends on `leave` prop, stays in route). Verify: open dialog, save, cancel.
8. Run `bun check` and fix all type errors.
9. Delete now-unused local `FieldError` definitions in `new.tsx` and `$id.tsx`.

## Tests

No existing form tests in the repo. Verification via manual browser smoke test per `CLAUDE.md` UI-change guidance. If desired later, a Vitest unit test for `useFormServerErrors` would be the highest-value target (pure logic, no DOM).

## Edge Cases

- **camelCase key conversion**: existing code lowercases only the first letter (`'Name' → 'name'`). Keep behaviour identical, not "fix" it.
- **`form.AppField` rerender cost**: bound components subscribe to their own field, same as today; no regression.
- **`SelectField` value type**: generic over option value type so `UserRole` round-trips without casts at the call site.
- **`EditLeaveDialog` `form.reset` on prop change**: untouched — that logic stays in the route since it depends on `leave` prop.
- **`hasClientSideError`** continues to live in `lib/form-utils.ts`: only `SubmitButton` calls it; not deleted.
- **`CheckboxField` value coercion**: shadcn `Checkbox` `onCheckedChange` emits `boolean | 'indeterminate'`; coerce indeterminate→false on change.

## Assumptions

- Field-component set: `TextField`, `NumberField`, `SelectField`, `TextareaField`, `CheckboxField`, `DateField`. RadioGroup/Switch deferred until first caller.
- Date strategy: native `<input type="date">`. Calendar/Popover deferred.
- Keep native `<select>` inside `SelectField` (matches current routes); can swap to shadcn `Select` later without touching callers.
- New file location `components/form/` (alongside `components/ui/`).
- Manual browser verification is sufficient; no Vitest required in this pass.
