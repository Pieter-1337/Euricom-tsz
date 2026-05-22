# Implementation: Contract task subform pre-save resurrect/restore UX

## Status

Complete

## Steps

| Step | Status | Notes |
| ---- | ------ | ----- |
| 1. Add `originalArchivedId` to `contractTaskFormSchema` | ✓ Done | |
| 2. Strip `originalArchivedId` in `toUpdateRequest` | ✓ Done | |
| 3. Subform: derive archived list from form state, pass `originalArchivedId` on Bring back, disable when already brought back | ✓ Done | Moved archived section inside `form.Field` block to read `field.state.value` without needing `useStore` |
| 4. Edit form: pass full archived list (no pre-filter) | ✓ Done | Already correct — no code change needed |

## Files Changed

- `packages/web/src/features/contracts/schemas.ts` — added optional `originalArchivedId: z.string().optional()` to `contractTaskFormSchema`
- `packages/web/src/features/contracts/server-fns.ts` — `toUpdateRequest` now destructures and drops `originalArchivedId` before sending to backend
- `packages/web/src/features/contracts/components/contract-task-subform.tsx` — archived section moved inside `form.Field` block; `broughtBackIds` set derived from `field.state.value`; archived list filtered by that set; Bring back sets `originalArchivedId`; Bring back disabled when id already in set

## Test Results

TypeScript type check: passed (0 errors)

No automated UI tests exist for this component.

## Deviations from Plan

- `contract-edit-form.tsx`: plan listed it as a file to touch, but the existing code already passes the full archived list (`contract.tasks.filter(t => t.deletedAt !== null)`). No change was needed.
- `pushFieldValue` is no longer called from the subform (uses `field.pushValue` inside the `form.Field` callback instead). The property remains on the `AppForm` type for compatibility with the `as never` cast but is unused.

## Open Items

None. Verification steps 1–7 from the plan can be confirmed manually by opening a contract with archived tasks.
