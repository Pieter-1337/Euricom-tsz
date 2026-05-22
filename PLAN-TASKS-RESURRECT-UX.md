# Plan — Contract task subform: pre-save resurrect/restore UX

## Problem

On the contract edit page (`packages/web/src/features/contracts/components/contract-task-subform.tsx`), the Archived list is derived from the loader's `contract.tasks` (server snapshot) and lives **outside** the form state. Consequences:

- "Bring back" pushes a new active row to `form.tasks` but the archived list stays frozen → the task appears in **both** sections until save.
- Cancel/Reset does not restore archived-side state.
- No way to undo a Bring back before save.

## Goal

Make the Tasks subform's active+archived split entirely a function of form state. Bring back / X / Add / Reset must behave coherently before save without round-tripping to the server.

## Model change

Extend `ContractTaskFormValue` in `packages/web/src/features/contracts/schemas.ts`:

```ts
const contractTaskFormSchema = z.object({
  id: z.string().nullable(),
  name: z.string().min(1, 'Task name is required').max(256),
  rate: z.number().positive('Rate must be greater than 0'),
  originalArchivedId: z.string().optional(), // INTERNAL — stripped before submit
});
```

`form.tasks` stays the source of truth for the **active** set. `originalArchivedId` is set only on rows produced by Bring back, and lets X reverse the operation.

## Behaviour matrix (per row in `form.tasks`)

| Origin            | `id`  | `originalArchivedId` | X click                                         | Sent to backend       |
| ----------------- | ----- | -------------------- | ----------------------------------------------- | --------------------- |
| Existing active   | guid  | —                    | filter row → backend soft-deletes on save       | `{ id, name, rate }`  |
| Fresh "Add task"  | null  | —                    | filter row                                      | `{ id: null, name, rate }` |
| Bring back        | null  | guid                 | filter row → archived row reappears             | `{ id: null, name, rate }` (backend resurrects by name) |

## Render

- **Active list**: `form.tasks` (unchanged).
- **Archived list**: derived live from `contract.tasks.filter(t => t.deletedAt !== null)` **minus** `originalArchivedId` values currently in `form.tasks`. Use `form.Subscribe` (or `useStore(form.store, ...)`) inside the subform so the archived list reacts to Bring back / X immediately.
- **Disable Bring back** for any archived row whose id is already in the brought-back set (prevents double-push).

## Cancel / Reset

Already works once `originalArchivedId` lives on form state: `form.reset()` restores `form.tasks` → derived brought-back set is empty → archived list shows everything again.

## Save

`packages/web/src/features/contracts/server-fns.ts` → `toUpdateRequest`: strip `originalArchivedId` when mapping:

```ts
tasks: form.tasks.map(({ originalArchivedId: _drop, ...t }) => ({ id: t.id, name: t.name.trim(), rate: t.rate }))
```

After successful save, `form.reset(value)` sets the working state as the new baseline. `router.invalidate()` refetches the loader; the next render gets fresh `contract.tasks` from the server, and the derived archived list recomputes correctly (resurrected rows no longer have `deletedAt`; newly-removed rows now do).

## Edge cases / decisions

1. **Bring back → rename → X**: row carries `originalArchivedId = X`. X click restores X to archived with its **original** name + rate (the rename is discarded). Matches the mental model of "undo the bring back".
2. **Bring back same archived row twice**: prevent by disabling/hiding the Bring back button when the id is already in the brought-back set.
3. **Add fresh task with same name as an archived one**: backend resurrects by name regardless (per PRD #3 user story #24). UI doesn't need to detect this — next loader refresh straightens it out. Punt.
4. **Edit name of a brought-back row to match a different archived row's name**: don't attempt to switch which archived id the row links to. Backend resurrect-by-name still works on save; UI doesn't try to be cleverer.

## Files touched

- `packages/web/src/features/contracts/schemas.ts` — add `originalArchivedId` to `contractTaskFormSchema` (optional, stripped before submit).
- `packages/web/src/features/contracts/components/contract-task-subform.tsx` — subscribe to form state for archived filtering; pass `originalArchivedId` on Bring back; disable Bring back when already brought back.
- `packages/web/src/features/contracts/server-fns.ts` — `toUpdateRequest` strips `originalArchivedId`.
- `packages/web/src/features/contracts/components/contract-edit-form.tsx` — pass the **full** archived list to the subform (no pre-filter); subform owns the filtering.

## Out of scope

- Backend changes — projection and reconciliation already handle this. No EF/migration work.
- Issue #10 authorization layer — separate slice.
- The integration test `GetContractById_AfterSoftDeleteTask_IncludesArchivedTaskInDto` already added speculatively to `ContractEndpointsTests.cs` — leave it; it guards the GET projection regardless of this UX work.

## Verification

1. Open contract with one archived task and one active task.
2. Click Bring back on archived → active list grows by one; archived list loses that row.
3. Click X on the brought-back row → archived list shows it again (original name + rate, even if user renamed in active).
4. Click Bring back, then Cancel (form reset) → archived list shows it again.
5. Click Bring back, edit name/rate, Save → server resurrects by name, page reloads, archived list no longer contains it, active list has it with the new name/rate.
6. Click X on a server-active task without saving, then Bring back any archived → both changes coexist in form state.
7. Save with mixed changes (remove A, bring back B, add C) → atomic backend reconciliation; refreshed view reflects new server truth.
