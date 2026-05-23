# Plan: Timesheets + Time Entry

**Status:** grill complete. Ready for `/matt-to-prd`.

Spans both requirement docs:
- `docs/product/requirements/time-entry/time-entry.md` (week editing grid, submit-for-approval)
- `docs/product/requirements/timesheets/timesheets.md` (month read-only overview)
- Downstream reader: `docs/product/requirements/leave-overview/leave-overview.md`

## Hypothesis (refined)

Original: "same backend domain, two FE views". Refined: **one new `Timesheets` module** owning two booking entities and a week-level lifecycle. Two FE views (time-entry weekly editing grid, timesheets monthly overview) plus a third downstream reader (Leave Overview yearly).

## Decisions taken

### Module shape — one new module: `Timesheets`

- Both requirement docs (`time-entry/`, `timesheets/`) → one backend module. Honours ADR-0001 *broadly* (the ADR says "broadly mirroring", not strictly).
- Volatilities.md predicts a later split into `TimeEntryManager` + `TimesheetAggregator`; today they stay together.

### Domain shape — Shape 4 (two distinct children)

```
Timesheets module
└── TimesheetWeek (aggregate root, per UserId × IsoYear × IsoWeek)
    ├── Status              Draft | Submitted | Approved
    ├── TimeEntries[]       children, ref ContractTaskId (work hours)
    └── LeaveBookings[]     children, ref LeaveTypeId   (leave hours)
```

- **Two distinct entities, NOT polymorphic.** Work hours produce billable time via `ContractTask.Rate`; leave hours deduct from `UserLeave` allowance. Different validations, readers, future fields (half-day flag, leave-specific approval, accrual deduction). Volatilities.md flags both as HIGH-volatility on *different* axes — modelling them as siblings keeps each clean.

### Aggregate pattern — Option A (week-as-root)

- All writes go through `week.AddTimeEntry(...)`, `week.AddLeaveBooking(...)`, `week.Submit()`, `week.Approve()`.
- "You cannot edit an approved week" is **structurally enforced** by the aggregate boundary, not by handler discipline.
- Reads (Timesheets month, Leave Overview year) bypass aggregates via dedicated query handlers (flat SQL → DTO). Codebase precedent: `GetContractsPaged.cs`.
- Single-cell write cost: load ~30 children. Negligible.

### Cross-module references

- `TimesheetWeek.UserId` → Users via `IUsersAccessModule`
- `TimeEntry.ContractTaskId` → Contracts via `IContractsAccessModule`
- `LeaveBooking.LeaveTypeId` → LeaveTypes via `ILeaveTypesAccessModule`

### LeaveType + UserLeave placement — both extracted (settled)

`LeaveType` AND `UserLeave` extracted out of `Users` into a new module pair (`Tsz.Modules.LeaveTypes` + `.Contracts`). See **ADR-0002**.

Why both: Q9 (allowance enforcement) settled as **option α — hard enforce at every write**. Timesheets validators must query the user's allowance on every bulk-flush write of LeaveBookings. That makes Timesheets a *second consumer* of `UserLeave` (alongside the existing User admin form consumer), triggering ADR-0002. Extracting both together is one issue, not two.

Module name stays `LeaveTypes` for now (the catalog is the heavier concept inside). The new module owns:
- `LeaveType` (catalog: Verlof, ADV, Feestdag, …)
- `UserLeave` (per-user × per-year allowance)
- public facade `ILeaveTypesAccessModule` exposing both catalog and allowance queries

`Users` module loses both; CreateUser's auto-seed of `UserLeave` rows-per-LeaveType becomes a cross-module call (Users → LeaveTypes via the new facade) or is restructured to happen via a coordinator. To be settled when issue #1 is implemented.

This becomes **issue #1** in the breakdown: extract LeaveTypes + UserLeave with behaviour identical to today, no functional change. Then the Timesheets work builds on the clean surface.

### Timesheets month read (Q13) — settled

**Option α — on-demand query handler.** `GetTimesheetMonth(userId, year, month)` issues a flat join across TimesheetWeeks + TimeEntries + LeaveBookings for that user × month, projects to `TimesheetMonthDto`. Reads bypass aggregates (codebase precedent: `GetContractsPaged.cs`).

No projection table. No write-side maintenance. The day-/week-/month-totals and the task-/leave-type summaries are computed by the query handler. Cross-module names (taskName, contractName, customerName, leaveTypeName) are joined in via Contracts and LeaveTypes facades during projection so the FE renders without N+1 fetches.

Sketch DTO (field-detail to be refined in matt-to-issues):

```
TimesheetMonthDto {
  userId, year, month
  weeks: [{
    isoYear, isoWeek
    status                                  // Draft | Submitted | Approved
    days: [{
      date
      isBusinessDay                         // false on Sat/Sun/holiday
      timeEntries:    [{ taskId, taskName, contractName, customerName, durationHours }]
      leaveBookings:  [{ leaveTypeId, leaveTypeName, durationHours }]
      totalHours
    }]
    perTaskSummary:      [{ contractName, taskName, totalHours }]
    perLeaveTypeSummary: [{ leaveTypeName, totalHours }]
  }]
  monthTotalHours
}
```

Forward-compatible to Option β (projection table) later: the endpoint contract stays the same; the rollup is an implementation detail.

### Leaves in v1 scope (Q12) — settled

**Option C: both TimeEntry and LeaveBooking ship in v1, but sequenced.** TimeEntry slice lands first as the architecture validator; LeaveBooking layered in afterwards. Both delivered to v1.

Rough vertical-slice ordering (to be detailed by `matt-to-issues`):

1. **LeaveTypes + UserLeave extraction** (refactor, no user-visible change). Settles the dependency graph. ADR-0002 first + second application.
2. **Workdays module + Holiday seed** (infra). Required before any booking write.
3. **Timesheets module scaffold + TimesheetWeek aggregate + bulk PUT (TimeEntry children only) + GET single week + FE week grid with task rows only.** First end-to-end slice.
4. **Submit / Approve / Reopen lifecycle.** Three states, admin-only approval.
5. **LeaveBooking children + extend bulk PUT + allowance enforcement + add leave rows to FE grid.** Leaves arrive.
6. **Timesheets month read view + FE month route.** Read-side feature.

Each slice independently shippable. Slices 1 and 2 are pure refactor/infra (no user-visible behaviour change) but still ship on their own.

### Hotkeys (Q11) — settled

Pure FE. `d` → `8.00`, `h` → `4.00`, `del` → cell cleared (row absent from bulk-flush body, server diff removes prior row). Zero backend involvement, zero API surface change. Lives in the cell-input component.

### Weekend/holiday enforcement (Q10) — settled

**Backend enforces.** Bulk-flush validator rejects bookings whose `Date.DayOfWeek ∈ {Sat, Sun}` or whose `Date ∈ Workdays.Holiday` table. Truth at the boundary, FE convenience only.

**New module: `Tsz.Modules.Workdays` + `.Contracts`** — mirrors LeaveTypes shape (ADR-0002 second application). Owns:
- `Holiday` entity (`Date DateOnly`, `Name string`, `Country string`, `Type enum`)
- `IWorkdaysAccessModule.IsBusinessDay(date) → bool`
- Migration with `HasData`-seeded BE 2026–2028 holidays

Consumer in v1: Timesheets validator. Future consumers: LeaveOverview (Prio 2 — "indication of school & work holidays"), future calendar features.

**v1 scope explicit non-goals:** No live `openholidaysapi.org` HTTP call. No background refresh job. No admin "Refresh holidays" endpoint. No multi-country (Country column is forward-compat; BE only). No school holidays (distinct from work holidays in the API; out of scope).

**Future increment:** Live API integration replaces the seeder with an HTTP-fetching seeder + background job. Facade and table shape don't change.

Becomes **issue #2** in the breakdown (after LeaveTypes extraction). Independently shippable; Timesheets work depends on it.

### Allowance enforcement (Q9) — option α (settled)

**Hard enforce on every LeaveBooking write.** The bulk `PUT /timesheet-weeks/.../bookings` validator computes, for each proposed LeaveBooking:

```
existing-year-consumption = Σ DurationHours of this user's existing LeaveBookings 
                              with same LeaveTypeId in the target year
proposed-consumption      = Σ DurationHours of proposed LeaveBookings 
                              with same LeaveTypeId in the target year
days-equivalent           = (existing + proposed) / DefaultDayHours   (8h for v1)
```

If `days-equivalent > UserLeave.TotalDays` → reject with `TimesheetErrors.LeaveAllowanceExceeded`. Per-LeaveType, per-year.

**Cross-module query:** `ILeaveTypesAccessModule.GetUserLeaveAllowance(userId, leaveTypeId, year)` returns `decimal?` (`TotalDays`).

**`TotalDays = null` semantics for v1: "unlimited" — no enforcement.** Rationale: today the schema allows null and there's no documented intent. "Unlimited" is the less-surprising default; if HR wants "zero allowance, must be set per user" semantics later, that's a `MissingUserLeave` validation rule added separately.

Consumption query is intra-module (Timesheets owns LeaveBookings). The validator builds one cross-module call per (user, leaveTypeId, year) combination present in the body — typically 1-2 per week.

### LeaveBooking shape — duration-only, day derived (settled)

`LeaveBooking` carries only `DurationHours decimal(3,2)`, same shape as `TimeEntry`. No `IsHalfDay` flag.

"Days" is a *derived* concept: `daysDeducted = DurationHours / DefaultDayHours`, with `DefaultDayHours = 8.0` as a module-level constant for v1. When `LeaveOverview` shows balances in days and `UserLeave.TotalDays` is checked against consumption, the division happens in the query handler — not on the row.

Future-proof for volatilities.md's "part-time / FTE working pattern" axis: changing `DefaultDayHours` from constant to `User.WorkingPatternHoursPerDay` is a one-line query-handler edit, no schema change.

### Hours storage (settled)

**`DurationHours decimal(3,2)`** on both `TimeEntry` and `LeaveBooking`. Valid values: `{0.25, 0.50, 0.75, …, 8.00}` enforced by a FluentValidation rule. Display format `HH:MM` is FE-only.

Rationale: `ContractTask.Rate` is already decimal — `Duration × Rate` stays in decimal arithmetic. Decimal hours is the lingua franca for consulting timesheets and exports.

### Write API shape (settled)

- **Shape A: bulk flush on navigation.** FE holds the week's edits as optimistic local state. On nav-away / week-change / beforeunload, it fires a single `PUT /timesheet-weeks/{userId}/{year}/{week}/bookings` with **the whole week's bookings** as the request body. Backend diffs against current persisted state inside the aggregate and applies adds/updates/deletes in one transaction. Returns the canonical week.
- The resource IS the week; the verb is PUT; the body represents the desired state.
- No per-cell endpoint. No granular ops list.
- Submit and Approve are separate endpoints (`POST /timesheet-weeks/{userId}/{year}/{week}/submit`, `POST .../approve`, `POST .../reopen`).
- Crash-recovery is not in v1 scope. Forward-compatible to Shape C (periodic batch flush) without breaking the API.

### Approval flow (settled)

- **State machine: `Draft → Submitted → Approved`** (three states on `TimesheetWeek.Status`).
- **Editing while Draft is free.** Entries are persisted in Draft state — saving an entry does NOT submit the week. Submit is a separate explicit action.
- **Submit** transitions the week from `Draft → Submitted`. Only the week's owning consultant submits.
- **Approve** transitions the week from `Submitted → Approved`. **Admin role only** in v1. ClientManager-as-approver is deferred (a week can span multiple client managers — punt the multi-CM resolution).
- **Reopen** transitions the week back to `Draft`. **Admin only.** Consultants cannot unsubmit themselves; the system can correct mistakes via admin.
- Once `Approved`, edits are blocked at the aggregate boundary (`TimesheetWeek` rejects child mutations unless `Status == Draft`).
- The two greens in `timesheets.md` map to: light = Submitted, dark = Approved.

`timesheets.md`'s "Out of Scope: Timesheet approval/signing" line is interpreted as: formal signing / multi-step / audit-grade approval is out of scope. The simple submit-approve-reopen lifecycle above IS in scope (per `time-entry.md` Prio 1).

### Leans on Q4a / Q4b

- **Q4a — booking validation lives in Timesheets validators.** They call out to `IContractsAccessModule` / `ILeaveTypesAccessModule`. Mirrors existing pattern (Contract validators consulting `IUsersAccessModule`).
- **Q4b — "selectable tasks for week W" list owned by Contracts.** `IContractsAccessModule.GetSelectableContractTasksForConsultantInWeek(consultantId, year, week)`. Timesheets endpoints proxy. Volatilities.md places `ContractSelectionEngine` in the Contracts column.

## Open questions

None — all grill questions Q1–Q13 resolved.

## Terminology

Captured in `CONTEXT.md` at the repo root. Highlights:
- **TimeEntry** — work-hour booking, child of `TimesheetWeek`, refs `ContractTaskId`.
- **LeaveBooking** — leave-hour booking, child of `TimesheetWeek`, refs `LeaveTypeId`.
- **TimesheetWeek** — aggregate root, one per (user × ISO week). Owns its bookings + `Status`.
- **Timesheet** — *not* a persisted aggregate. Month-level read view; produced by a query handler.
- "WeekApproval" — *not* a separate entity. Folded into `TimesheetWeek.Status`.

## Rejected alternatives (don't re-grill these)

- **Shape 1 — single polymorphic `TimeEntry` (discriminator: Work | Leave) in a new `TimeEntry` module.** Bloats one entity with two semantically different concepts; predicted to diverge. Doesn't honour the two requirement docs.
- **Shape 2 — `LeaveBooking` in Users module alongside `LeaveType`/`UserLeave`.** Precedent for "leave stuff in Users" turned out to be a packaging accident in `983df08`, not a design conviction. Cross-module lock (WeekApproval in Timesheets locking rows in Users) is awkward.
- **Shape 3 — single polymorphic `Booking` in a `Timesheets` module.** Same divergence problem as Shape 1, just renamed.
- **Aggregate option B — flat `TimeEntry` root + sibling `WeekApproval`.** Defensible, but loses structural enforcement of the lock invariant. Reads-bypass-aggregates pattern means option A doesn't actually cost cross-week reads.
- **LeaveType inside Timesheets module.** Creates Users → Timesheets dependency cycle via `UserLeave.LeaveTypeId`. Forbidden DAG.

## Workflow position

Per `docs/agents/workflow-manual.md`:
- [x] `/matt-grill-with-docs` (in progress — paused here)
- [ ] resolve open decisions + open questions
- [ ] `/matt-to-prd` — publish PRD as draft tracker issue
- [ ] human review on tracker
- [ ] `/matt-to-issues` — break into vertical slices (likely: LeaveTypes extraction as issue #1, then Timesheets module scaffold, then TimeEntry write, then LeaveBooking write, then Timesheets month read, then FE editing grid, then FE month view)
- [ ] human review
- [ ] `/commit` ADRs + CONTEXT.md updates

## Next steps

1. ~~Grill (Q1–Q13)~~ — complete.
2. ~~CONTEXT.md + ADR-0002~~ — written.
3. `/matt-to-prd` — publish PRD as a draft issue on the tracker.
4. Human review on the tracker.
5. `/matt-to-issues` — break into the 6 vertical slices sketched in Q12.
6. `/commit` — bundle CONTEXT.md + ADR-0002 + plan.md into a doc-only commit.
