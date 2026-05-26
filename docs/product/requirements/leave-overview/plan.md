# Plan: Leave Overview

**Status:** grill complete. Ready for `/matt-to-prd`.

Backing requirement: `docs/product/requirements/leave-overview/leave-overview.md`. Upstream readers it now reads from: `LeaveTypes` (`UserLeave` allowance) and `Timesheets` (`LeaveBooking`) and `Workdays` (`Holiday`).

## Hypothesis (refined)

A read-only year-grid view of one user's leave year — calendar shading per `LeaveBooking` + per public `Holiday`, plus a balance panel summarising allowance vs consumption.

Originally framed as a downstream reader of the Timesheets PRD, possibly its own backend module. Refined: **no new backend module.** Leave Overview is a Client (in volatilities.md terms) composed FE-side from three independent endpoints, each owned by the module that owns the underlying data. The page composes; the backend does not.

## Decisions taken

### Architecture — δ: FE composition (settled)

No backend "LeaveOverview" module. No coordinator endpoint. The page issues three GETs in parallel via TanStack Query and merges:

1. `UserLeave` allowance for the year — owned by `LeaveTypes`
2. `LeaveBooking`s for the year — owned by `Timesheets`
3. Public `Holiday`s for the year — owned by `Workdays`

Rationale:
- No use case in v1 requires the backend to hand back a single composite. Three caches with independent invalidation are *better* than one mega-DTO.
- A backend "merged" endpoint would have to live in `Timesheets` (it already legally depends on `LeaveTypes` + `Workdays`); putting it there bloats Timesheets with what is really a UI projection.
- Putting it in `LeaveTypes` is **forbidden** — would create a `LeaveTypes → Timesheets` cycle since `Timesheets → LeaveTypes` already exists (allowance enforcement, leave-type name projection). Forbidden DAG.
- The same composition is reused by the admin user-edit form's existing `LeaveOverviewSection` so it can finally show real Taken/Balance. One hook (`useLeaveSummary(userId, year)`), two consumers.
- Reversible: if a real use case for a merged backend payload appears later, we can add it in Timesheets without breaking the three feeders.

### Endpoints (three, all year-scoped)

#### E1 — Self-service auth on existing leaves endpoint

`GET /users/{userId}/leaves?year=YYYY` (already exists, currently `RequireAdmin`).

Relax to **"admin OR userId == self"** via a new authorization policy `RequireAdminOrSelf`, mirroring the existing `RequireAdminOrAnyClientManager` shape:

- `AuthorizationPolicies.RequireAdminOrSelf` constant.
- `RequireAdminOrSelfRequirement` + `RequireAdminOrSelfAuthorizationHandler`. The handler resolves the current user via `ICurrentUserResolver` and reads the `userId` route value via `IHttpContextAccessor.HttpContext.GetRouteValue("userId")`. Succeeds if `user.HasRole(Admin) || user.Id == routeUserId`.
- Applied via `.RequireAuthorization(AuthorizationPolicies.RequireAdminOrSelf)` on the GET endpoint group.

`PUT /users/{userId}/leaves` stays `RequireAdmin` — editing allowance totals is admin-only by design; consultants don't grant themselves leave.

No new endpoint. No `/me/...` alias. Reused by E2.

#### E2 — Year-scoped leave bookings (new, in Timesheets module)

`GET /timesheet-weeks/{userId}/leave-bookings?year=YYYY`

```
LeaveBookingDto[] {
  date            DateOnly
  leaveTypeId     Guid
  leaveTypeName   string         // projected via ILeaveTypesAccessModule
  durationHours   decimal(3,2)
}
```

- Flat list. Sorted by `(date, leaveTypeId)` server-side — stable rendering for the multi-type-day case.
- Lives next to `GetTimesheetMonth.cs` in `Tsz.Modules.Timesheets/Features/`.
- Same `RequireAdminOrSelf` policy as E1.
- One EF query, projection via `ILeaveTypesAccessModule` for name.

Why not extend `GetTimesheetMonth` to year-scope: drags `TimeEntry` rows the page doesn't need; the year-summary use case is leave-only.

#### E3 — Year-scoped holidays (new, in Timesheets module)

`GET /workdays/holidays?year=YYYY` (route path keeps the user-facing `/workdays/...` namespace — route ≠ module ownership)

```
HolidayDto[] {
  date    DateOnly
  name    string
  type    HolidayType
}
```

- Any authenticated user (holidays are global reference data).
- Intra-module `GetHolidaysInYearQuery` + handler in `Tsz.Modules.Timesheets/Features/`. Lives next to `GetTimesheetMonth.cs`.
- See "Module layout" below for why `Holiday` lives in Timesheets.

### Module layout — `Holiday` + business-day rules belong in Timesheets

`Timesheets` owns `Holiday` + `HolidayType` + the business-day rule used by the bulk-flush validator. There is no separate `Workdays` module.

Per **ADR-0002**, a sub-concept gets promoted to its own module pair when it gains a *second* cross-module C# consumer. Holiday data has one such consumer today (Timesheets' weekend/holiday-rejection validator). Leave Overview consumes holiday data over HTTP, not via a C# cross-module call, so it does not trigger ADR-0002. With a single consumer, the concept stays inside its host module.

If a real second cross-module C# consumer arrives later (e.g. Contracts needing locale-aware business-day validation, or a future calendar feature), the standard ADR-0002 extraction is a small refactor.

### Backend dead-field cleanup (settled)

`UserLeaveDto` currently carries `TakenDays?` and `BalanceDays?` slots hardcoded to `null` (`UserLeaveDto.cs:7-33`). With δ chosen, the FE derives Taken/Balance from E2 — these slots stay dead forever.

- Drop both fields from the record.
- Update `Project` expression and both `ToDto` overloads.
- Regenerate FE schema (`bun --filter web gen:api`).
- Existing `LeaveOverviewSection` rendered `—` from null today; tomorrow it renders Taken/Balance from a `useLeaveSummary` hook fed by E1+E2.

### Computations (FE-side)

#### Taken (per LeaveType per year)

```
taken[leaveTypeId] = Σ booking.durationHours / WorkdayCapacity
                                              (= 8.0 v1, see CONTEXT.md)
```

Same formula as the write-side allowance enforcement in `ApplyTimesheetWeekBookings` validator. Same constant. Aligned by intent, not by accident.

#### Balance (per LeaveType per year)

| `DefaultAllowed` | Total | Taken | Balance |
|---|---|---|---|
| Limited | `userLeave.totalDays` | `Σ/8` | `total - taken` |
| Unlimited | `—` | `Σ/8` | `—` |
| NotAllowed | (not rendered — no real data uses this enum value in v1) | | |

#### Total row (panel footer)

Sum across **Limited** rows only — Total / Taken / Balance all numeric. Unlimited rows excluded from the sum. Synthesized rows (Feestdagen) excluded.

### Feestdag duality — settled

`Feestdag` is **not** a `LeaveType` in v1. The seed has only Verlof / ADV dagen / Anciënniteit / Ziekte. The mock's "Feestdagen" balance row is **FE-synthesized** from `Workdays.Holiday` count in the year. Calendar shading for public holidays comes from E3 directly.

If a future LeaveType named "Feestdag" is created (admin LeaveType-editor doesn't exist today), it renders like any other row — no special-case code.

### Year navigation + empty state — settled

- **Unbounded** prev/next year navigation.
- If E1 returns `[]` for the requested year → render an "no leave configured for YYYY" banner over an otherwise-rendered calendar (weekends + holidays still shade, bookings render if any exist).
- **No lazy auto-seeding** of `UserLeave` rows on read. Annual rollover is a separate write-side concern, out of scope.

### Multi-type day rendering — settled

When a single date has bookings of multiple LeaveTypes:

- 1 type → solid color (with half-day visual if `durationHours < 8`)
- 2 types → top half = first by `(date, leaveTypeId)`, bottom half = second
- 3+ types → neutral hatched "mixed" badge

Backend sorts E2 by `(date, leaveTypeId)` for stable rendering across reloads.

### Color per LeaveType — settled

**FE-derived from a fixed palette**, indexed by leave-type-id (stable hash → palette slot). No `Color` column on `LeaveType`. No admin colour-editor.

If an admin colour-editor ever lands, add a nullable `Color` column and have the FE fall back to the palette when null. Reversible without breaking the FE.

### Click-to-week (Prio 2, in v1) — settled

Clicking a day in the calendar navigates to `/timesheets/week/{isoYear}/{isoWeek}` where `(isoYear, isoWeek) = getISO8601WeekOfDate(clickedDate)`. Pure FE wiring; the route exists.

Edge case: Jan 1 may belong to ISO week 52/53 of the previous year (and Dec 31 may belong to ISO week 1 of the next). Use a tested ISO-week util on the FE; do not roll our own.

### Sidebar navlink — must not be forgotten

Every new top-level FE route needs a corresponding entry in the primary sidebar nav, which lives in `packages/web/src/routes/_protected.tsx` (the `<NavLink>` list at lines 133-137 today: Home / Time Entry / Users / Customers / Contracts).

For Leave Overview: add a new `<NavLink to="/leaves" icon={…} label="Leave overview" collapsed={collapsed} />` between Time Entry and Users (or wherever the design lands). Visible to all authenticated users (no `isAdmin` / `canManageClients` guard).

This step is recurringly forgotten and breaks the page's discoverability — the feature is shipped but unreachable without typing the URL.

### What we explicitly do NOT do in v1

- No live `openholidaysapi.org` integration; rely on the existing seeded BE holidays (2026-2028).
- No school holidays (a `HolidayType` value may exist but isn't rendered distinctly).
- No admin "view another user's overview" page (read endpoints support it via the userId path, but no FE route).
- No annual-rollover `UserLeave` seeding job.
- No backend-computed Taken / Balance.
- No new `LeaveOverview` backend module.

## Side findings (not in this scope, but capture so they aren't lost)

1. **`LeaveAllowed.NotAllowed` is a phantom state.** Enum value exists; zero rows use it. The write-side allowance validator treats `TotalDays = null` as "unlimited", which would silently permit bookings against any future NotAllowed type. If admin LeaveType-creation is ever built, this gap re-opens — needs a separate validator rule.

2. **Annual `UserLeave` rollover** is unimplemented. `SeedUserLeavesAsync` (in `LeaveTypes`) fires only on user create, current year. A user opening 2027 next January gets empty allowance rows. The Leave Overview's empty-year banner handles the read side, but the underlying gap remains.

3. **`UserLeaveDto.TakenDays` / `BalanceDays` dead slots** — dropped as part of this slice.

## Open questions

None — all branches resolved.

## Terminology updates

To be captured in `CONTEXT.md`:

- **Leave Overview** — read-only year-grid view of one User's leave year. NOT a persisted entity. NOT a backend module. Composed FE-side from three endpoints owned by `LeaveTypes`, `Timesheets`, and `Workdays`. Sibling to `Timesheet` (the month read view) in being a view-only domain term.
- **Feestdagen (in Leave Overview)** — synthesized balance-panel row counting `Workdays.Holiday` rows for the year. Not backed by `LeaveBooking`s and not a `LeaveType`.

## Rejected alternatives (don't re-grill)

- **New `LeaveOverview` backend module.** Pure aggregator — no entities of its own. Overhead without payoff.
- **Move the merged endpoint into `Timesheets`.** Legal direction (Timesheets → LeaveTypes already exists), but creates two endpoints with overlapping `UserLeave`-shaped output (LeaveTypes' raw `/users/{id}/leaves` and Timesheets' "year summary"). Confuses readers.
- **Move it into `LeaveTypes`.** Forbidden — creates `LeaveTypes → Timesheets` cycle.
- **Endpoint-layer coordinator in `Tsz.Api`.** Breaks the convention that each module owns its endpoints.
- **`/me/leaves` alias for self-service.** Adds a second endpoint declaration sharing one handler. A reusable `RequireAdminOrSelf` policy is simpler and matches existing auth infra (`RequireAdmin`, `RequireAdminOrAnyClientManager`).
- **Inline self-check in each handler.** Repetitive; doesn't reuse the policy infrastructure already in place.
- **Backend-stored `LeaveType.Color`.** Schema change for a vanity feature. Reversibly add later if admins ever want to tune.
- **Lazy `UserLeave` auto-seeding on read.** Hidden write triggered by a read; complicates the "read-only page" mental model.
- **Year navigation clamped to seed range or to years-with-data.** Adds an extra fetch or hardcodes a window that rots next year. Unbounded + empty banner is simpler and honest.

## Workflow position

Per `docs/agents/workflow-manual.md`:

- [x] grill — complete
- [x] plan written — this file
- [ ] CONTEXT.md update (Leave Overview term)
- [ ] `/matt-to-prd` — publish PRD as draft tracker issue
- [ ] human review on tracker
- [ ] `/matt-to-issues` — break into vertical slices

## Next steps

1. ~~Grill~~ — complete.
2. Update `CONTEXT.md` with the Leave Overview term + Feestdagen clarification.
3. `/matt-to-prd` — publish as draft issue.
4. Human review.
5. `/matt-to-issues` — break into vertical slices. Suggested ordering:
   1. Backend: new `RequireAdminOrSelf` authorization policy + apply to existing `GET /users/{id}/leaves` (E1 auth relax). Drop dead `TakenDays`/`BalanceDays` from `UserLeaveDto` (`bun --filter web gen:api` to regenerate schema).
   2. Backend: add E2 (year-scoped leave bookings in Timesheets) + E3 (year-scoped holidays in Timesheets — `Holiday` + `HolidayType` + business-day rule move into Timesheets as part of this slice; no DB migration since the `Holidays` table name is unchanged).
   3. FE: `computeLeaveSummary` pure function + `useLeaveSummary` hook + retrofit existing admin `LeaveOverviewSection`.
   4. FE: new self-service `/leaves/{year}` (or similar) route — year grid + balance panel + click-to-week — **plus a new `<NavLink>` in `_protected.tsx`**.
   Each slice independently shippable.
