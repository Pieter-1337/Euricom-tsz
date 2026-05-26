# Plan: Split `timesheet-week-grid.tsx` into smaller pieces

## Goal

Break the 821-line `timesheet-week-grid.tsx` into a small orchestrator plus a handful of focused components and hooks, collapsing duplicated task/leave structure into one generic primitive. Pure restructure — no behaviour changes.

## Context

`packages/web/src/features/timesheets/components/timesheet-week-grid.tsx` does six jobs in one file:

1. **State**: bookings + saved snapshots + row arrays + UI state + error (~50 LOC)
2. **Autosave / flush**: `flush()`, `useBlocker`, `beforeunload` (~70 LOC)
3. **Cell editing**: `setTaskCell`/`setLeaveCell` + keydown/change/blur, duplicated for task and leave (~90 LOC)
4. **Row ops**: add/remove for task and leave — near-duplicates (~40 LOC)
5. **Lifecycle + navigation**: submit / approve / reopen / week navigation (~70 LOC)
6. **JSX**: header bar, status badge, banner, table (thead / task rows / leave rows / tfoot), two add-row popovers (~400 LOC)

Task rows vs leave rows and the two "Add" popovers are structurally identical — they differ only in styling (default vs amber) and which `Map`/handlers they read from. Collapsing them is the biggest readability win.

The route file (`routes/_protected/_authenticated/timesheets/week/$year/$week.tsx`) imports `TimesheetWeekGrid` by name; the spec file (`timesheet-week-grid.spec.tsx`) drives the public component, not its internals — its three tests must keep passing unchanged.

## Files

Create folder `packages/web/src/features/timesheets/components/week-grid/` and put everything below into it:

- `timesheet-week-grid.tsx` — create (move from current path): orchestrator only (~120 lines). Holds lifecycle handlers, `navigateToWeek`, derives `dayTotals`/`weekTotal`/`hasDayCapacityError`/`errorMessage`, renders banner + status badge inline, composes the children.
- `week-actions-bar.tsx` — create: top header bar. Left: prev/next/today/calendar week navigation. Right: status text ("Unsaved changes" / "Saving..."), Save, Submit, Approve, Reopen.
- `week-table.tsx` — create: full table. `<thead>` and `<tfoot>` (day totals) inline; `<tbody>` maps `BookingRow` instances for task rows then leave rows, plus the empty-state row.
- `booking-row.tsx` — create: one generic row component with `variant: 'task' | 'leave'`. Cell rendering (readonly / editable `<input>` with focus/change/blur/keydown handling) lives inline in this file.
- `add-row-popover.tsx` — create: one generic popover. Used twice in the orchestrator (task and leave) via a `variant` prop.
- `use-week-bookings.ts` — create: hook. Owns `taskBookings`, `leaveBookings`, `savedTaskBookings`, `savedLeaveBookings`, `taskRows`, `leaveRows`. Exposes `isDirty`, `setTaskCell`, `setLeaveCell`, `addTaskRow`, `addLeaveRow`, `removeTaskRow`, `removeLeaveRow`, and `markSaved()` for the flush hook to call on success.
- `use-week-flush.ts` — create: hook. Owns `flush()`, `useBlocker`, `beforeunload`, `isFlushing`, `flushError`. Takes `{ userId, year, week, taskBookings, leaveBookings, isDirty, markSaved }` and returns `{ flush, isFlushing, flushError }`.
- `timesheet-week-grid.spec.tsx` — move from current path to `week-grid/`. No selector changes.

Delete the old files at their original paths:

- `packages/web/src/features/timesheets/components/timesheet-week-grid.tsx`
- `packages/web/src/features/timesheets/components/timesheet-week-grid.spec.tsx`

Update the import in:

- `packages/web/src/routes/_protected/_authenticated/timesheets/week/$year/$week.tsx` — change `from '#/features/timesheets/components/timesheet-week-grid'` to `from '#/features/timesheets/components/week-grid/timesheet-week-grid'`.

## Steps

1. Create `packages/web/src/features/timesheets/components/week-grid/`. Move `timesheet-week-grid.tsx` and `timesheet-week-grid.spec.tsx` into it. Update the route import. Run typecheck + spec to confirm green baseline.
2. Extract `use-week-bookings.ts`. Lift the four booking `Map`s, the two row arrays, derived `isDirty`, the `setTaskCell` / `setLeaveCell` callbacks, and `addTaskRow` / `addLeaveRow` / `removeTaskRow` / `removeLeaveRow`. Expose `markSaved()` that copies current bookings into the saved snapshots. Wire the orchestrator to use it. Spec still passes.
3. Extract `use-week-flush.ts`. Lift `flush`, `useBlocker`, `beforeunload`, `isFlushing`, `flushError`. On successful save, call `markSaved()` from the bookings hook. Keep the same error-mapping logic (leave allowance / day capacity / fallback). Spec still passes.
4. Extract `booking-row.tsx` with `variant: 'task' | 'leave'`. Props: `row`, `days`, `isDraft`, `getValue(date) → number | undefined`, `onCellChange(date, value | null)`, `cellInputs`, `editingCell`, `onCellFocus(key, stored)`, `onCellBlur(key, stored)`, `onCellKeyDown(date, e)`, `onRemove()`. Cell render branches on `isReadOnly = !isDraft || !d.isBusinessDay`. Tailwind classes parametrised by variant (default vs amber). Orchestrator passes one set of handlers per variant. Spec still passes.
5. Extract `add-row-popover.tsx` with `variant: 'task' | 'leave'`. Props: `items` (with `id`/label/optional subtitle), `onAdd(item)`, `emptyMessage`, `triggerLabel`, `open`, `onOpenChange`. Orchestrator renders two instances. Spec still passes (button name `/^Add task$/i` still matches).
6. Extract `week-table.tsx`. Contains `<table>` with thead/tbody/tfoot inline; takes `days`, `taskRows`, `leaveRows`, `taskBookings`, `leaveBookings`, `dayTotals`, `weekTotal`, `isDraft`, `status`, cell-edit state and handlers, and `onRemoveTaskRow` / `onRemoveLeaveRow`. Renders empty-state row when `taskRows.length === 0 && leaveRows.length === 0`. Spec still passes (`data-testid="day-total-..."` preserved).
7. Extract `week-actions-bar.tsx`. Props: `year`, `week`, `status`, `isDirty`, `isFlushing`, `isLifecycleLoading`, `hasDayCapacityError`, `isDraft`, `isAdmin`, `onNavigateWeek(y, w)`, `onPickWeek(date)`, `onSave`, `onSubmit`, `onApprove`, `onReopen`. Encapsulates the prev/next/today buttons + calendar popover and the right-side action buttons.
8. Shrink the orchestrator to ~120 lines. It now: calls both hooks, derives `dayTotals`/`weekTotal`/`hasDayCapacityError`/`errorMessage`, holds the local `editingCell` + `cellInputs` state (kept in orchestrator so cross-cell coordination still works without a context), holds `handleSubmit`/`handleApprove`/`handleReopen`/`navigateToWeek`, renders inline status badge (~9 lines) and error banner (~5 lines), and composes `<WeekActionsBar>`, `<WeekTable>`, two `<AddRowPopover>`s.
9. Run `bun run typecheck` and the spec. Confirm three tests pass unchanged.

## Tests

- The existing three specs in `timesheet-week-grid.spec.tsx` must keep passing without modification: red-cell indicator, normal totals under cap, and save short-circuit + banner when over cap.
- No new tests required. Optional follow-up (not in scope): unit test for `useWeekBookings` covering "add row + remove row → `isDirty === false`".

## Edge Cases

- `useBlocker` and the `beforeunload` listener must close over the latest `flush` callback — they live inside `use-week-flush.ts` alongside it, so this stays correct.
- `cellInputs` and `editingCell` stay in the orchestrator (not in `BookingRow`) so focusing a new cell can flush the previously-edited one's raw string into a normalised value. Moving them into `BookingRow` would break that cross-cell coordination.
- `flushError` must clear at the start of each `flush()` call (current behaviour). Preserved inside the flush hook.
- The orchestrator's outer `key={`${year}-${week}`}` (set in the route) forces a remount on week change, which discards hook state and re-seeds from the new `initialData`. No code changes needed — just verify after the refactor.
- Status-dependent class names on the outer table container (`bg-green-50` for Submitted, etc.) move into `week-table.tsx`. Verify the visual still matches in Submitted / Approved states.
- The empty-state row's `colSpan` (`isDraft ? 10 : 9`) must stay in sync with the actual column count in `week-table.tsx`.

## Assumptions

- The task/leave row + popover unification (one component with `variant` prop) is desired. If the two paths diverge later we split then.
- Subfolder `week-grid/` is acceptable; it's the first subfolder under `components/` for the timesheets feature but signals "internals of one feature component".
- No behaviour changes: same DOM structure where the spec relies on it (button names, `data-testid="day-total-..."`), same save / submit / autosave semantics.
- No new tests required to land the refactor; the existing spec is the safety net.
