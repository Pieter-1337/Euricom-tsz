# Changelog

## 2026-05-26

### refactor: drop Status column from the Timesheets list

- Remove the Status column from the bottom Timesheets list: it reflected
  internal week-approval, not customer document approval (out of scope), and
  was misleading. The list now shows Customer, Contract and the Download action.

### feat: split timesheet totals into approved and not-approved sections

- Add a "Not approved yet" section to the Timesheets totals panel below the
  approved one (built from Submitted + Draft weeks).
- Count the workdays headline as distinct business days with any booking per
  status, so Approved + Not approved add up to the booked workdays.
- Show hours (not days) in the per customer/leave-type breakdown rows to avoid
  the effort-vs-days confusion.

### feat: timesheet PDF export, colour-coded entries, Holidays rename

- Add a printable monthly timesheet document (browser print-to-PDF) reached
  from the aggregated Timesheets list, with Date / Task / Hours / Days columns
  and combined totals.
- Aggregate the Timesheets list to one row per customer+contract for the month
  (combined status) instead of one row per week.
- Colour-code leave entries by leave type (matching the balance panel) and map
  holidays to amber across the month calendar, week grid, add-row dropdowns and
  the approved-totals panel; worked entries stay green.
- Show hours on leave entries in the month calendar, like worked entries.
- Rename the synthesized "Feestdagen" row to "Holidays".
- Make Timesheets the landing page: redirect / to /timesheets, move it first in
  the sidebar, and drop the redundant Home page.

### feat: refine leave overview, timesheet totals and form field layout

- Count leave bookings in the timesheet month totals so approved leave days
  are included alongside worked entries (e.g. 9/9 workdays).
- Render the leave balance totals as a real table footer row so it aligns
  with the columns, and move the balance panel beside the year calendar.
- Split leave calendar day cells into equal bands per entry, add hover
  tooltips listing every entry with its hours, and a pointer cursor.
- Lay out the admin leave table in even quarter-width columns with uniform
  row heights (number input cells collapsed to match text rows).
- Add a shared StaticField so read-only fields keep consistent spacing in
  the user and customer edit forms.
- Add a `side` prop to Tooltip and use it for the sidebar collapse button
  and collapsed nav links.

### feat: self-service Leave overview page with year grid

Consultants get a new **Leave overview** page (linked in the primary sidebar, visible to everyone) showing their leave year at a glance:

- A 12-month compact calendar shades weekends, public holidays, and booking days. Each leave type gets a stable colour; half-days render as a partial fill; days with two leave types split top/bottom and three-or-more show a hatched "mixed" cell.
- Today's cell is highlighted. Clicking any day jumps to that week's Time Entry page.
- A balance panel lists Total / Taken / Balance per leave type (with `—` for unlimited types), a Feestdagen row, and a Limited-only totals row.
- Previous / next year navigation is unbounded; a year with no configured leave shows a "No leave configured" banner over an otherwise-working calendar.

### feat: Leave Overview Slice 3 — computeLeaveSummary + useLeaveSummary + retrofit admin form

New folder packages/web/src/features/leaves/ with:
- computeLeaveSummary pure function: builds Limited/Unlimited rows, synthesized
  Feestdagen row, and totals (Limited only). Taken = Σ durationHours / 8.
- useLeaveSummary(userId, year) hook: composes 3 TanStack Query calls
  (['user-leaves', userId, year], ['leave-bookings', userId, year], ['holidays', year])
  and returns raw bookings + holidays (for Slice 4 calendar) and computed rows/totals.
- server-fns.ts wrapping fetchUserLeavesForYear, fetchLeaveBookingsForYear,
  fetchHolidaysForYear using existing apiClient pattern.

API layer:
- packages/web/src/api/leaves.ts — re-exports LeaveBookingForYear, HolidayDto types.
- packages/web/src/api/leaves.server.ts — getLeaveBookingsForYear, getHolidaysForYear
  calling the Slice 2 endpoints.
- packages/web/src/api/schema.ts — adds /api/timesheet-weeks/{userId}/leave-bookings
  and /api/workdays/holidays paths + LeaveBookingForYearDto, HolidayDto, HolidayType
  schemas. Also adds TimesheetMonth* DTOs that were missing (pre-existing typecheck
  failures on master from the Slice 2 PR).

Retrofit admin form:
- LeaveOverviewSection now consumes useLeaveSummary and renders real Taken/Balance
  values. Synthesized Feestdagen row appears in TableBody. TableFooter shows totals
  row (Limited only). Form indices unchanged — Feestdagen row is appended after the
  form-bound leave rows, not spliced in.

Tests (all passing):
- compute-leave-summary.spec.ts: 8 unit tests covering each AC scenario.
- use-leave-summary.spec.tsx: 2 hook tests with mocked server fns and QueryClientProvider.

Slice 3 of the Leave Overview PRD (#33). Fixes #36.

### feat: split Time Entry and Timesheets into separate pages

Reorganize routing and UI to separate the Time Entry (data entry) workflow
from the Timesheets (submission tracking) view. Key changes:

- Move /timesheets/week/* and /timesheets/month/* → /time-entry/* namespace
- Create new /timesheets overview page with month calendar, approved totals
  panel, and submitted timesheets list table
- Calendar shows days with color-coded entry bars (green/amber/dark), prev/
  today/next navigation, and approved status indicators
- Add fast Radix-based tooltip component (100ms delay) replacing slow native
  browser tooltips
- Update navbar to reflect distinct routes: Time Entry vs Timesheets

Internal: update all navigation references, remove unused params in grids.

### feat: year-scoped leave bookings and holidays endpoints

Two new read endpoints for the upcoming year-at-a-glance Leave Overview:

- `GET /timesheet-weeks/{userId}/leave-bookings?year=YYYY` returns the user's `LeaveBooking` rows for the year (sorted by date then leave-type id, projecting `leaveTypeName`). Gated by the `RequireAdminOrSelf` policy.
- `GET /workdays/holidays?year=YYYY` returns the year's public holidays from the seeded 2026-2028 BE list. Available to any authenticated user.

The `Tsz.Modules.Workdays` module has been folded into `Tsz.Modules.Timesheets` (per ADR-0002: Holiday data has only one cross-module C# consumer, so it lives inside its host module). The `IWorkdaysAccessModule` facade is gone; the bulk-flush validator now calls an intra-Timesheets `BusinessDayService` directly. No DB migration was needed — the `Holidays` table is unchanged.

### feat: consultants can read their own leave allowance

Consultants no longer need admin access to read their own `UserLeave` rows. `GET /users/{userId}/leaves?year=YYYY` now accepts any caller whose authenticated user id matches the route, in addition to admins reading any user. `PUT /users/{userId}/leaves` stays admin-only — consultants cannot grant themselves leave.

Powered by a new reusable `RequireAdminOrSelf` authorization policy that resolves the route's `userId` and compares it to the current user, or short-circuits when the user is in the `Admin` role.

Wire-shape cleanup: `UserLeaveDto` drops the dead `TakenDays` and `BalanceDays` slots that were never populated. The admin user-edit form already hardcodes `—` placeholders in those columns and is unaffected. Consumers computing Taken/Balance now derive them from `LeaveBooking` reads on the frontend.

### chore: fix worktree line-ending churn and strengthen orchestration contracts

Add `* text=auto eol=lf` to `.gitattributes` to normalize all text files to LF on checkout, preventing fresh worktree creations (with `core.autocrlf=true`) from expanding files to CRLF and leaving ~hundreds of phantom line-ending modifications versus the LF-stored index.

Strengthen `/app-do-work` step 7 completion contract: a successful reviewer pass is NOT a substitute for opening a PR. Workers must push the branch, open the PR, verify it exists, and report its URL as the final line of output. Commit the work only if the working tree shows only the intended changes.

Strengthen `/app-do-prd` scheduling loop (§4a-verify): after a worker returns, verify the PR exists before advancing. Treat a missing PR as a worker failure, not a success-to-advance on dependent slices.

### feat: agent-driven auto-merge and one-attempt iterate budget for /app-do-prd

Add `--auto-merge` flag (default off): when enabled, the orchestrator
itself merges each PR once readiness conditions hold — CI green, every
review thread resolved, no reviewer-pass findings outstanding, mergeable
state — by running `gh pr merge`. Imperative, not GitHub's declarative
auto-merge.

Iterate mode now handles every kind of blocker (CI red, unresolved
comments, unaddressed reviewer notes, merge conflicts), not just CI red,
with a budget of one extra attempt beyond the initial implementation.
If that single iterate attempt still doesn't bring the PR to a mergeable
state, the slice waits for human attention (failure mode under the
default `continue-siblings` policy).

Also: rename SKILL section 6½ to clean numbering (Auto-merge §6, Failure
recovery §7, Final report §8); strip cross-references between the
workflow docs in the autonomous doc body so each doc stands on its own;
restructure Stacking & merge cadence around `--auto-merge=false/true`
rather than default-vs-alternative framing.

### feat: add /app-do-prd orchestrator skill; expand /app-do-work with iterate + reviewer

Split the autonomous workflow into two skills so single-issue runs stay
clean. `/app-do-prd <PRD>` is the new orchestrator: parses the child
issues' `## Blocked by` DAG, spawns one worktree-isolated worker per
ready slice, handles CI-red iterate re-spawns, recovers from failures
with one diagnostic re-spawn and `--on-failure=continue-siblings` as
the default. `/app-do-work <issue>` keeps its single-issue surface but
gains two behaviours: it detects an existing open PR for the issue and
enters iterate mode (push to the existing branch instead of starting
fresh), and it runs a `reviewer` sub-agent pass before commit (default
on, `--reviewer=false` to opt out). Cross-link the three workflow docs
so the autonomous surface command (`/app-do-prd`) is consistent
everywhere.

### docs: document the autonomous PRD-implementation workflow

Add `docs/agents/workflow-autonomous.md` describing the multi-issue
orchestrator: PRD or single-issue target, DAG-driven scheduling from
`## Blocked by`, worktree isolation per slice, per-issue agent
auto-routing (with recursive sub-spawning of specialists, no
general-purpose fallback), iterate mode on open PRs (auto re-spawn on
CI red, manual re-launch for review comments), and `--on-failure`
defaulting to continue-siblings rather than halt. Cross-link
`workflow-automatic.md` and `workflow-manual.md` with a shared three-
mode "Relationship to the other workflows" view. Promote `/simplify`
from optional to required in the manual chain. Fix three stale
references to `/matt-grill-me` (skill is `/matt-grill-with-docs`).

### refactor: tighten timesheet week grid dirty state and error display

Derive `isDirty` from a saved-bookings snapshot instead of a manual flag,
so adding a task row then removing it no longer leaves the form dirty.
Surface the day-capacity error banner immediately (not just after a save
attempt), disable Save and Submit while a day exceeds the cap, drop the
generic "Could not save changes" inline text, and shorten the row picker
labels to "Add task" / "Add leave". Rename the sidebar entry "Timesheets"
to "Time Entry". Extract `areMapsEqual` to `lib/utils.ts`.

### refactor: scaffold leave overview foundations and split week grid

Two bundled efforts to prepare for the Leave Overview view:

Backend — Leave Overview groundwork:
- Rename seeded LeaveType names from Dutch to English (Annual leave,
  ADV days, Seniority leave, Sick leave) via migration.
- Seed Belgian 2025 public holidays into the Workdays.Holiday table.
- Expose `GetDayKindsAsync` on `IWorkdaysAccessModule` returning
  per-date business-day / holiday metadata in one round-trip, so the
  FE Leave Overview can render the year calendar without N queries.
- Update Timesheets handlers and tests to consume the new API.

Frontend — week grid split (no behaviour change):
- Move `timesheet-week-grid.tsx` into a `week-grid/` subfolder and
  break the 823-line file into an orchestrator plus focused parts:
  `week-actions-bar`, `week-table`, `booking-row`, `add-row-popover`,
  and hooks `use-week-bookings` / `use-week-flush`.
- Collapse duplicated task / leave row + popover structure into one
  generic component parametrised by `variant`.
- Existing spec passes unchanged.

Docs:
- Add Leave Overview term to CONTEXT.md (FE-composed view, not a
  persisted entity; sibling to Timesheet).
- Capture rationale that "Feestdag" is a Workdays.Holiday, not a
  LeaveType — the balance panel row is FE-synthesized.

### refactor: simplify timesheet UI with shared AddRowPopover

Remove variant prop from AddRowPopover and consolidate styling by:
- Moving add row buttons to week-actions-bar navExtras
- Removing variant-specific (task/leave) styling differences
- Replacing title attribute with InfoTooltip for holidays
- Unifying cell styling across task and leave rows

Add info-tooltip component for displaying holiday names on hover.

## 2026-05-24

### Timesheets — day-capacity rule

Saving a timesheet week now blocks when any single day's combined time and leave bookings exceed eight hours. The over-cap day is flagged in red directly in the grid so consultants see the problem before they hit Save.

- Each day-total cell shows `{total}h / 8h max` in red whenever its combined time + leave booking total exceeds eight hours.
- Clicking Save (or navigating away) with an over-cap day no longer sends a request; the grid surfaces the same "One or more days exceed the daily capacity" message inline.
- The same rule is enforced on the backend so out-of-band callers cannot bypass it: the bulk save endpoint returns `400` with code `ERR_TIMESHEET_DAY_CAPACITY_EXCEEDED`.

### Sidebar restructure, timesheet grid UX, and day-capacity design docs

Customers and contracts are now top-level navigation items; admin retains users only. The timesheet week grid gets a UX pass, and the day-capacity rule is captured in CONTEXT.md, the time-entry PRD, and a new ADR (implementation pending).

- Move `/admin/customers` and `/admin/contracts` to top-level `/customers` and `/contracts`. Route files moved under `_protected/_authenticated/` so they inherit the centralized auth guard; sidebar restructured to surface them outside the admin zone.
- New `/timesheets` index route redirects to the consultant's current ISO week.
- Tighten the admin layout role check to Admin-only (was Admin or ClientManager); customers/contracts have their own canManageClients guards.
- Timesheet week grid: replace hand-rolled Add-task / Add-leave dropdowns with shadcn `Popover`; add row delete; reset per-week state on week change via React `key`; fix calendar chevron stacking-context bug.
- Add `WorkdayCapacity` term to `CONTEXT.md`: one daily working-hours constant driving both the per-day booking cap and leave-day-equivalence arithmetic.
- Add a per-day cap bullet to `docs/product/requirements/time-entry/time-entry.md`: total booked time per day (tasks + leave combined) cannot exceed 8 hours.
- Add `docs/adr/0003-aggregate-vs-validator-rule-placement.md`: domain invariants that depend only on aggregate state live in the aggregate; cross-module and cross-aggregate concerns live in the validator. Day-capacity is the rule's first application.

### Nav — Add "My Timesheet" sidebar link

Add a 'My Timesheet' sidebar link that opens the consultant's current-week timesheet, so the new timesheets feature is discoverable from the navigation.

### Refactor — centralize authenticated-route guard in `_authenticated` layout

Centralize the authenticated-route guard in a single _authenticated layout so admin and timesheets routes no longer each need their own redirect-when-unauthenticated check. URL paths are unchanged; this is a code-quality refactor only.

### Bug fix — SSR auth crash in timesheets routes

Fix SSR-time crash on the new timesheets week and month routes when the user is not yet authenticated — they now redirect cleanly instead of throwing, matching the admin-route pattern.



### Timesheets — month overview

Consultants and admins can now view a month overview of timesheets with daily totals, per-task and per-leave-type summaries, and week-status colour coding. Clicking a day jumps to that week's editing grid.

- New route `/timesheets/month/{year}/{month}` displays a read-only month view.
- Each ISO week whose Monday falls inside the calendar month is shown as a colour-coded card: light green for Submitted, dark green for Approved, neutral for Draft.
- Each day cell shows its daily total hours; clicking a business-day cell navigates to the week editing grid for that day's ISO week.
- Per-task and per-leave-type summaries are shown per week and aggregated in a monthly summary panel.
- Month navigation (previous / next / today) buttons allow browsing any period.
- Admin users can view any consultant's month; non-admin users are restricted to their own data (403 otherwise).

### Timesheets — leave rows + allowance enforcement

Consultants can now record leave hours alongside work hours in the weekly grid; year-based allowance limits are enforced server-side and excess bookings are rejected with a clear error.

- A new "Add leave row" button opens a picker of active leave types. Leave rows are visually distinguished (amber tint) from task rows.
- Leave cells support the same hotkeys (`d` = 8 h, `h` = 4 h, `Del` = clear) and 15-minute increments as task rows. Weekend and holiday cells are read-only.
- Day totals and the weekly total now include leave hours.
- The server enforces per-year leave allowances: if the total booked hours for a leave type in the calendar year would exceed the user's allowance, the PUT is rejected with `ERR_TIMESHEET_LEAVE_ALLOWANCE_EXCEEDED`. An inline banner displays the rejection message.
- A null allowance means unlimited leave — no enforcement is applied for those types.
- The bulk PUT body now carries `timeEntries` and `leaveBookings` as separate arrays.

### Cleanup — cubic-dev-ai review findings (#20, #23)

- `Holiday.DeletedAt` now has a `private set`, matching the entity encapsulation convention.
- `TimesheetErrors.NotFound` now maps to `ErrorCategory.NotFound` (HTTP 404) instead of `ErrorCategory.Validation` (HTTP 400).
- `handleSubmit` in the timesheet grid now sets the loading state before `flush()`, preventing a race where the Submit button was briefly re-enabled during the async save.
- `flush()` now returns `Promise<boolean>`; `handleSubmit` aborts and surfaces an inline error if the save failed, preventing a submit call on un-persisted edits.

### Timesheets — submit / approve / reopen lifecycle

- Consultants can now submit a Draft timesheet week. The Submit button in the grid header posts `POST /api/timesheet-weeks/{userId}/{year}/{week}/submit` and refreshes the page with the new Submitted status.
- Admins see an Approve button when a week is Submitted and a Reopen button when a week is Submitted or Approved. These call `POST .../approve` and `POST .../reopen` respectively.
- Non-owners receive 403 on submit; non-admins receive 403 on approve and reopen.
- Attempting to approve a Draft week or reopen a Draft week returns 400 with a typed error code.
- Bulk-PUT bookings against a non-Draft week returns 400 (`ERR_TIMESHEET_NOT_DRAFT`) — the grid cells are read-only when the status is Submitted or Approved.
- The grid background is tinted green (light for Submitted, deeper for Approved) to visually signal the locked state.
- A status badge (pill) is displayed above the grid when the status is non-Draft.

### Timesheets — weekly time-entry grid

- Consultants can now navigate to `/timesheets/week/{year}/{week}` to record their working hours per contract task for any week.
- The grid shows one row per contract task, with editable cells per day (Mon–Sun). Weekend and public-holiday cells are read-only.
- Cell hotkeys: `d` sets 8 h, `h` sets 4 h, `Del` clears the cell. Values are validated to 15-minute increments (0.25–8.00 h).
- Per-row and per-day totals are computed client-side; the week total is shown in the bottom-right corner.
- Add a task row via the "Add task row" picker — the list is filtered to contract tasks active for the logged-in consultant in the selected week.
- Changes are automatically flushed as Draft to the backend when navigating away from the week or closing the tab — no explicit save button.
- Week navigation: prev / next arrow buttons, a "Today" shortcut, and a calendar picker to jump directly to any week.
- A stubbed "Submit" button is visible (no-op until the Submit/Approve slice lands).
- New module pair `Tsz.Modules.Timesheets` + `Tsz.Modules.Timesheets.Contracts` with `TimesheetWeek` aggregate, `TimeEntry` children, and EF migration `AddTimesheets`.
- Endpoints: `GET /api/timesheet-weeks/{userId}/{year}/{week}` (returns empty Draft when no record exists) and `PUT /api/timesheet-weeks/{userId}/{year}/{week}/bookings` (whole-week desired state, server-side diff).
- Bulk PUT validates: dates inside the requested week, business days only, eligible contract tasks, valid duration increments, and Draft status.

## 2026-05-23

### Leave Types module

- Leave types and per-user leave allowances now live in their own `LeaveTypes` module, separate from the Users module.
- Creating a user still automatically assigns a leave balance row for each active leave type — behaviour is identical to before.
- The User admin form's leave management endpoints (`GET/PUT /api/users/{userId}/leaves`) work identically; request and response shapes are unchanged.
- Internal code that previously referenced leave types directly through the Users module now goes through the new `ILeaveTypesAccessModule` facade.

### Workdays module

- The backend now knows which dates are Belgian business days, enabling downstream Timesheets validation to reject submissions on weekends or public holidays.
- New module pair `Tsz.Modules.Workdays` + `Tsz.Modules.Workdays.Contracts` exposes `IWorkdaysAccessModule.IsBusinessDay(DateOnly) → bool`.
- Returns `false` for Saturday, Sunday, or any seeded BE `Holiday` row; `true` otherwise.
- Migration `AddHolidays` creates the `Holiday` table with a unique index on `(Country, Date)` and `HasData`-seeds all 10 Belgian public holidays for 2026, 2027, and 2028 (no HTTP integration — the seeder is the cache for this slice).

docs: capture Timesheets grill — CONTEXT.md, ADR-0002, plan.md

- Plan-side output of the matt-grill-with-docs session for the
  Timesheets + Time Entry feature. The three new docs go in together
  because they only make sense as one set.
- CONTEXT.md (new, repo root) — first repo-wide glossary. Establishes
  the booking-domain vocabulary (TimeEntry, LeaveBooking,
  TimesheetWeek, Timesheet-as-view, LeaveType, UserLeave), the
  relationships, and the flagged ambiguities the grill resolved
  (notably: "WeekApproval" is not a separate entity; it is
  TimesheetWeek.Status).
- docs/adr/0002-promote-on-cross-module-reference.md — codifies the
  rule "extract a sub-concept to its own module when a second consumer
  shows up across a module boundary". First applied to LeaveType
  (extracted from Users together with UserLeave); the same rule
  justifies the new Workdays module planned in slice 2.
- docs/product/requirements/timesheets/plan.md — full grill decision
  history Q1–Q13, rejected alternatives kept so future readers do not
  re-grill the same questions, and the rough 6-slice migration order.
- Implementation work itself lives on the tracker, not in this commit:
  PRD Pieter-1337/Euricom-tsz#12; six AFK slices issued as #13–#18,
  each labelled enhancement + ready-for-agent.

chore: remove temporary planning documents

- Clean up planning and implementation notes that were used to track
  the contract task subform restoration feature. These temporary
  documents are no longer needed as the work is complete.

## 2026-05-22

refactor: support admin or any client manager zone authorization

- Replace RequireClientManagerAuthorizationHandler with
  RequireAdminOrAnyClientManagerAuthorizationHandler to enable admin and
  any client manager (not just zone-specific) to access contracts and
  customer resources. Updates API endpoints, tests, and web UI forms to
  reflect new authorization semantics.

refactor: extract scoped-filter composition, resurrect contract task UX

- Consolidates repeated ownership-check logic in handlers and validators
  into ScopedFilter.ComposeAsync and ScopedRequestValidator<T> base class.
  Migrates 9 validation sites (3 query handlers, 4 update/delete validators,
  1 create validator) to use the shared helpers, eliminating 4-line duplicate
  blocks per location.
- Extends contract task subform to drive archived filtering from form state.
  Bring back / X / Reset / Save now cohere without server round-tripping until
  save. originalArchivedId field tracks resurrections; stripped before submit.
- Add Tsz.Infrastructure.Auth.Validation.ScopedFilter (static helper).
- Add Tsz.Infrastructure.Auth.Validation.ScopedRequestValidator<T> base
  (RuleForOwnedEntity, RuleForSelfAssignedManager extension methods).
- Migrate GetCustomerById, GetContractById, CustomerExistsQueryHandler
  to use ScopedFilter.ComposeAsync.
- Migrate DeleteCustomerValidator, DeleteContractValidator,
  UpdateCustomerValidator, UpdateContractValidator to inherit base class.
- Migrate CreateCustomerValidator.NonAdminAssignsSelf to use
  RuleForSelfAssignedManager.
- Extend contractTaskFormSchema with optional originalArchivedId.
- Rewrite contract-task-subform.tsx: archived list derived from form state,
  Bring back button disabled when id already brought back.
- Strip originalArchivedId in toUpdateRequest before backend submit.
- Update 9 test files for new constructor/base-class shapes.
- All 252 unit + 107 integration tests pass.

refactor: eliminate expression-tree construction in ownership filters

- Reshape OwnershipPolicy<T> to hold a Func<Guid, Expression<Func<T, bool>>>
  factory instead of an Expression<Func<T, Guid?>> selector. This moves
  equality-expression construction from runtime (Expression.Equal /
  Expression.Lambda in DataScopeAccessor) to compile-time (normal C# lambda
  at policy declaration sites), eliminating reflection-adjacent code and
  gaining full type safety.
- DataScopeAccessor.OwnershipFilterAsync: drop Expression.* calls, invoke
  factory directly. Simplified from ~10 lines to 3.
- GetCustomersPagedHandler.ScopePolicy, GetContractsPagedHandler.ScopePolicy:
  declare factory as `userId => c => c.ClientManagerId == userId`.
- Add AuthorizationPolicies.AdminRoleName = "Admin" constant (used by new
  ScopedRequestValidator).
- Add Tsz.Infrastructure.Auth.Validation.ScopedFilter: static ComposeAsync
  helper (compose ownership filter with additional predicate).
- Add Tsz.Infrastructure.Auth.Validation.ScopedRequestValidator<TRequest>:
  abstract base class for owned-entity + self-assignment rules.
  RuleForOwnedEntity now takes Func<Guid, Expression<Func<TEntity, bool>>>
  (no runtime expression construction).
- Migrate 3 query handlers (GetCustomerById, GetContractById,
  CustomerExistsQueryHandler) to use ScopedFilter.ComposeAsync.
- Migrate 4 validators (DeleteCustomer, DeleteContract, UpdateCustomer,
  UpdateContract) to inherit ScopedRequestValidator and use
  RuleForOwnedEntity + RuleForSelfAssignedManager.
- Add ScopedFilterTests and ScopedRequestValidatorTests (unit tests for new
  helpers).
- Update DeleteContractValidatorTests fixture for new base-class constructor.
- All 262 unit (252 + 10 new) + 107 integration tests pass.

## 2026-05-21

feat: contracts API — CRUD endpoints, domain model, persistence

- Add Contracts module with endpoints for create, get, update, delete,
  and list operations with paging and search
- Implement Contract aggregate with ContractTask and ContractConsultant
  owned collections; Contract.ApplyTasks handles task reconciliation
  including resurrect-by-name
- Add validators for contract and task operations; reject empty task
  names, invalid rates, duplicate active names, and stale references
- Create migrations for Contracts, ContractConsultants, and ContractTasks
  tables with soft-delete support
- Wire module into IoC and API; expose cross-module queries for
  contract existence checks and client manager resolution
- Add comprehensive tests for handlers, validators, domain logic,
  and cross-module query handlers

feat: manage contract tasks (#9) — add, edit, remove, resurrect-by-name

- Admins can manage the task list on a contract end-to-end: add a
  task, edit its name and rate, remove it (soft-delete), and re-add
  a task with a previously-removed name (case-insensitive) to
  resurrect the archived row in place with the new rate and casing
- `ContractTask` owned collection persisted to a new `ContractTasks`
  table; `Contract.ApplyTasks` aggregate mutator implements the
  five reconciliation cases including resurrect-by-name
- `UpdateContractCommand` extended with an optional `tasks` array;
  validator rejects empty names, names over 256 chars, non-positive
  rates, duplicate active names, and unknown payload ids
- `ContractDto.tasks` now lists both active and archived rows;
  `ContractSummaryDto.activeTaskCount` reflects the live count
- Frontend contract edit page has a Tasks subform with an editable
  Active section and a read-only Archived section with a
  "Bring back" affordance to resurrect by name

feat: contracts list page with paged search & filters

- Admins can open the Contracts page and browse a paged table of
  contracts with subject, customer, period, and live task /
  consultant counts (both 0 until later slices populate them)
- Free-text search matches both contract subject and customer name
- New filters let admins narrow contracts by an "active on" date or
  a specific customer; clear-all chip resets them
- Soft-deleted contracts are excluded from the default view; toggle
  to view only deleted

chore: update app-do-work and validate skills for tracker issues

Updated both skills to accept tracker issue references (#N) in addition
to local file paths. Enhanced documentation to clarify resolution of
parent issues (e.g. PRDs) that carry constraints and context.

## 2026-05-20

docs: configure GitHub as issue tracker for matt-* skills

Wire Pieter-1337/Euricom-tsz up as the issue tracker used by the
matt-to-prd, matt-to-issues, and matt-triage skills, replacing the
missing /setup-matt-pocock-skills bootstrap step.

- Add docs/agents/tracker.md documenting the tracker repo, gh
  commands, and the canonical->GitHub label mapping (1:1, no
  translation needed)
- Reference tracker.md from CLAUDE.md so skills pick it up
- Clarify in workflow-manual.md that /matt-to-prd publishes a
  draft issue and that the tracker itself is the review surface
- Allow `gh label create*` in .claude/settings.json so the
  triage labels can be provisioned without re-prompting
- Fix app-do-work SKILL frontmatter name to match its skill name

The four missing triage labels (needs-triage, needs-info,
ready-for-agent, ready-for-human) were created on the GitHub repo
outside this commit.

refactor: extract domain modules to separate assemblies with boundary enforcement

Restructure packages/api from a monolithic single-assembly layout to a
modular monolith with csproj-enforced module boundaries. Single API host,
single DB context, and single schema remain unchanged; only code organization
evolves.

Key changes:
- Create Tsz.SharedKernel for value types (Countries enum)
- Create Tsz.Modules.Users (impl) + Tsz.Modules.Users.Contracts (public surface)
- Create Tsz.Modules.Customers (impl) + Tsz.Modules.Customers.Contracts (public)
- Move Users/Customers domain logic into their respective assemblies
- Add IModule abstraction for consistent registration and endpoint routing
- Refactor cross-module coupling: UpdateUserValidator now injects
  ICustomerDirectory instead of querying RepositoryFor<Customer>;
  CreateCustomerValidator now injects IUserDirectory instead of
  RepositoryFor<User>. Both directory interfaces provide minimal,
  stable contracts for cross-module checks.
- Update AppDbContext.OnModelCreating to apply configs from both module
  assemblies via ApplyConfigurationsFromAssembly
- Refactor Program.cs to instantiate modules explicitly and call their
  RegisterServices + MapEndpoints methods
- Update all test files to reference new module namespaces
- Fix malformed appsettings.json (missing comma and brace)

No schema migrations needed; existing migrations remain in Tsz.Api.

Tests: 159 unit tests pass, 61 integration tests pass.

refactor: implement universal soft-delete with named query filters

Make soft-delete a universal convention: IEntityBase extends ISoftDeletable,
so all entities have a DeletedAt property by default. Entities that don't use
soft-delete explicitly Ignore the property in EF configuration.

Name the soft-delete query filter "SoftDelete" to allow selective ignoring.
The paging repository now bypasses only this named filter when DeletedOnly is
set, instead of toggling all query filters.

Delete the RepositoryPaginationExtensions helper; paging logic is now
unified in EfCoreRepository.GetPagedAsync.

## 2026-05-19

feat: add client manager support to customers

Add optional ClientManagerId field to customers to track the assigned
client manager. Updates Customer entity, EF configuration, API
endpoints, and UI forms to support managing the client manager
assignment. Includes migration and comprehensive tests.

feat: support multiple roles per user

Refactor user model from single role to role collection. Update handlers,
validators, DTOs, and tests. Add UserRoleAssignment join table and
migration. Update frontend components to display and manage multiple roles.
Add multi-combobox and badge UI components.

feat: customers module — full CRUD with admin pages

Expand Customers from { Id, Name } stub to full CRUD with admin
parity to the Users module: auto-incremented Number, Address and
ContactPerson value objects, soft delete, paged list, and admin
create/edit pages.

Backend adds Address + ContactPerson value objects, Customer entity
with named mutators and DeletedAt query filter, CQRS slices
(GetById, GetPaged with keyset + sort map + searchable, Create with
Number = max+1, Update, soft Delete), admin-grouped endpoints, EF
migration with unique Number, and unit + validator + integration
tests.

Frontend adds customers API client, server fns, paged list using
ListShell + useListQuery (sortable columns, infinite scroll,
search, soft-delete filter), and create/edit pages with a shared
customer form. Adds NumberField (with spin buttons), ComboboxField,
SelectFieldKV; shadcn combobox/command/popover; countries catalog.

refactor: extract NumberInput/SelectInput/DateInput primitives

Pull the spin-button number input, the Select composition, and the
type=date input out of form-context into reusable UI primitives in
components/ui. Every *Field in form-context is now a thin adapter
(Label + primitive + FieldError) following the same shape as
TextField/CheckboxField.

Merges SelectField + SelectFieldKV: SelectInput accepts either a
list of string values or {value, label} pairs and normalizes them
internally. SelectFieldKV had zero call sites; removed.

feat: add customers module with list page

Add a new Customers aggregate with minimal data (id, name). Includes:
- Backend: Customer entity, EF IEntityTypeConfiguration, migration, DTO,
  GetCustomersQuery handler, and admin-gated /api/customers GET endpoint
- Frontend: customers list page with id + name table, feature folder
  structure, server functions, and sidebar nav entry
- Regenerate OpenAPI schema and route tree

style: apply linter formatting to web files

Reformat ~10 web files for line length and collapsing (printWidth 120).
Changes are purely formatting: no behavior change.

feat: add search field filtering to keyset pagination

Introduce PredicateBuilder and SearchableField abstractions to enable
flexible, type-safe search term filtering across EF Core queries. Users
can define searchable columns (string with Contains, or enums with name
matching) without rebuilding predicates manually.

Update KeysetQueryableExtensions to accept optional filter predicates.
Refactor GetUsersPaged and GetCustomersPaged endpoints to leverage
searchable fields for user-driven filtering. Add comprehensive tests
for keyset paging with search terms.

Also update product requirement docs to reflect search capability.

## 2026-05-18

feat: apply Euricom design system to app chrome

Replace the header and sidebar in _protected.tsx with the Euricom
Tech Tribes design: 56px charcoal header (brand-anchored in both
themes) with brandmark, wordmark, user pill, sign-out, and theme
toggle; collapsible eyebrow-sectioned sidebar (NAVIGATE / ADMIN)
that toggles between 240px expanded and a 64px icon rail, with
the collapse state persisted to localStorage. Default the app
to dark mode and silence the resulting hydration warning on
<html>. Add the Euricom palette + motion tokens to styles.css and
expose them as Tailwind utilities via @theme inline, load
Montserrat from Google Fonts, drop brandmark.svg and
grid-pattern.svg into public/, add the shadcn Separator primitive,
and save the durable design reference at packages/web/docs/
DESIGN.md (with a pointer from CLAUDE.md). Also add a
.worktreeinclude so new worktrees auto-copy .env, .env.local, the
local SQLite databases, and the mkcert local certs.

feat: validate web env vars via zod and centralize access

Add env.server.ts that parses process.env through a zod schema at
module load and exits the process on failure, so missing or
malformed env crashes at boot instead of producing runtime !-bang
unwraps deep in the auth flow. Server-only files (auth.server,
api-client.server) import the typed env object instead of reaching
into process.env. tests/setup.ts populates the required vars so
server modules can still be imported in tests. Drive-by: drop
unused ctx param in better-auth onError handler so the file
typechecks cleanly.

docs: add module decomposition adr and volatility watch-list

ADR-0001 records the choice of functional decomposition over
volatility-based for v1, with the trigger to revisit. Companion
volatilities.md walks each requirement through expected axes of
change, synthesises shared volatilities into a candidate
volatility-based decomposition (kept as reference, not built),
and lists early-warning signals for future extractions.

refactor: flatten api test projects out of tests subfolder

Move Tsz.Api.Tests and Tsz.Api.Tests.Integration up from
packages/api/tests/ to packages/api/, matching the layout of the
other projects (Tsz.Api, Tsz.Infrastructure) that sit directly
under packages/api. Update csproj ProjectReference paths, the
tsz.slnx solution (collapse /tests/ folder into /api/),
package.json scripts (test:api, test:api:int), and the skill docs
that hard-code the old paths.

refactor: drive list views with TanStack Table

Adopt @tanstack/react-table for column definitions, sort state, and
header rendering. Keep custom code only where the library does not
reach (react-query glue, IntersectionObserver sentinel, search
debounce, deletedOnly toggle).

useListQuery now holds TanStack SortingState instead of sortBy/
sortDir useState; it returns sorting + onSortingChange. The hook
translates SortingState to the server's sortBy/sortDir params via
a derived memo. enableMultiSort is off and onSortingChange refuses
empty sorting so the server always receives one sort key.

InfiniteTable instantiates useReactTable with manualSorting and
getCoreRowModel, then renders via flexRender. Loading/empty/error
states and the sentinel row are preserved. ListShell now takes a
columns: ColumnDef[] prop and forwards sorting state to the table
instead of accepting a { head, row } slot.

Replace SortableHeader (37 lines, render-prop based) with
SortableHeaderCell (~20 lines) that takes a TanStack Column and
toggles asc/desc via column.toggleSorting.

users-list.tsx declares a top-level columns array; column ids use
`satisfies UserSortKey` so typos are caught at compile time while
keeping the value typed as string for TanStack Table.

Update frontend-feature skill to show the columns/SortableHeaderCell
pattern instead of the old render-prop layout.

test: drop unused repo tuple from user validator test helpers

CreateUserValidatorTests and UpdateUserValidatorTests returned a
(validator, Mock<IRepository<User>>) tuple from BuildValidator, but
every call site discarded the repo with `_`. The mock is only needed
inside the helper to wire up the IUnitOfWork. Return just the
validator and drop the tuple destructuring at each call site.

refactor: reorganize web package into features/lib/hooks/server

Extract users-route code into src/features/users/ (schemas.ts,
server-fns.ts, components/). Routes under _protected/admin/users/
become thin composers: index/new just import the component;
$id keeps the loader and a thin EditUserPage that handles the
not-found branch and passes data as props.

Split src/lib/ into three folders by runtime target:
- src/lib/ — browser+server safe utils (cn, parseServerError,
  authClient, form-utils)
- src/hooks/ — shared React hooks (useListQuery,
  useInfiniteScrollSentinel, useFormServerErrors)
- src/server/ — server-only code (apiClient, better-auth instance,
  getSession/SessionUser, getCurrentUser, bun-sqlite types)

Update frontend-feature, frontend-form, and validate skills to
match the new layout. frontend-feature now shows the
features/<name>/ vertical slice and thin routes; frontend-form
places schemas/server fns/components in the feature folder and
shows a thin route that hands data as a prop.

chore: clean up skills and add worktree to gitignore

Remove create-auth-skill and associated eval workspace files. Update
implement skill with improved description. Add .claude/worktrees to
.gitignore to exclude temporary worktree directories.

## 2026-05-14

fix: toggle Active shows only active or only deleted, not both

Rename IncludeDeleted → DeletedOnly throughout (backend, frontend).
New semantics: Active ON (toggle checked) shows only active users
(default, DeletedOnly=false). Active OFF (toggle unchecked) shows
only soft-deleted users (DeletedOnly=true). Query filter applies
when false; when true, ignoreQueryFilters + filter to DeletedAt != null.

Backend: KeysetQueryOptions record, GetUsersPagedQuery, endpoint,
handler adds filter expression when DeletedOnly=true. Update tests
to reflect new behavior. All 57 unit tests pass.

Frontend: pagination types, users.server, useListQuery, route schema,
component toggle logic. Toggle semantic: checked={!deletedOnly}.

fix: polish paged users list — binding, search, and UI stability

Backend:
- Fix sortDir query binding: accept string, parse case-insensitively
  (minimal API doesn't use TypeConverter; "asc" → SortDirection.Asc)
- Add Role to searchable columns for filter support

Frontend:
- Sortable header: reserve icon space so columns don't shift on sort
- Toolbar: make flex-1 so "N items" pushes to far right; prevent wrap
- Debounce search 300ms to reduce request volume during typing
- Use keepPreviousData so results stay visible during refetch (no flicker)

feat: add paged users endpoint and IRepository.GetPagedAsync

Add GET /api/users/paged returning KeysetPage<UserDto> alongside the
existing GET /api/users (plain list, untouched). Add
IRepository<TEntity>.GetPagedAsync facade so handlers do not need
to import KeysetQueryableExtensions directly. Implement it in
EfCoreRepository as a one-liner delegation to ToKeysetPageAsync.
New GetUsersPaged slice: query record derives from KeysetQueryOptions,
validator subclasses KeysetQueryOptionsValidator with a three-column
SortMap (name/email/role), handler delegates to GetPagedAsync with
firstName/lastName/email searchable columns. 17 new unit tests
(10 handler, 6 validator) + 7 new integration tests covering
pagination, search, sort, soft-delete visibility, and 400 guard
cases for bad sortBy/cursor.

feat: paged user list with search/sort/infinite-scroll

Rewrite the admin user list to consume the new GET /api/users/paged
endpoint. Add getUsersPaged to users.server.ts (hand-typed against
a new /api/users/paged path added to schema.ts; uses the same
openapi-fetch client as all other server-side wrappers). The
existing getUsers plain-list wrapper is untouched.

Route rewrites index.tsx: loader removed; component uses
useListQuery with a fetchUsersPaged createServerFn as the
fetcher. Renders ListShell (ListToolbar + InfiniteTable)
with SortableHeader columns on Name, Email, Role; Active toggle
wired to includeDeleted; row click navigates to /admin/users/$id;
"New user" button kept in page header. Infinite scroll via
sentinelRef from useListQuery.

docs: update user-list plan for new paged endpoint

Rewrite docs/product/requirements/users/plan-list.md to reflect the
new approach: keep GET /api/users (plain IEnumerable<UserDto>)
unchanged for dropdown/lookup consumers, add GET /api/users/paged
returning KeysetPage<UserDto> for the admin overview. Update the
backend section to add a new GetUsersPaged slice alongside the
existing GetUsers, and reference the new IRepository.GetPagedAsync
facade that hides the keyset extension method from handlers.
Update the steps and test matrix to clearly delineate which surfaces
are new and which remain untouched.

feat: add keyset pagination primitives to Tsz.Infrastructure

New Common/Pagination/ folder under Tsz.Infrastructure with five files:
KeysetCursor (base64-url opaque cursor, version-gated decode),
KeysetPage<T> (items + nextCursor + total), KeysetQueryOptions record
with SortDirection enum, SortMap<TEntity> + SortColumn<TEntity> (case-
insensitive key lookup, AllowedKeys for validator), and
KeysetQueryableExtensions.ToKeysetPageAsync (8-step algorithm: search
via string.Contains/ToLower, cursor predicate via IComparable.CompareTo
so string columns work, ORDER BY sortCol + Id tiebreaker, LIMIT+1
cursor detection, parallel COUNT). KeysetQueryOptionsValidator<TQuery,
TEntity> base enforces universal rules (search ≤200, sortBy in map,
pageSize 1-200, cursor decodable). 40 new unit tests across three files
(cursor encode/decode, extensions against EF InMemory, validator rules).

feat: add list-base frontend primitives

Add keyset pagination types (KeysetPage, SortDir, KeysetQueryParams)
in api/pagination.ts. Add useListQuery (wraps useInfiniteQuery, holds
search/sort/includeDeleted state, flattens pages, wires sentinel) and
useInfiniteScrollSentinel (IntersectionObserver callback-ref hook) in
lib/. Add sortable-header, list-toolbar, infinite-table, and list-shell
components in components/list/. Add shadcn Switch primitive (hand-
written from radix-ui, matching existing component style) since the
CLI add could not run interactively. Add @tanstack/react-query to
package.json (was in bun.lock via transitive dep but not declared).

docs: rewrite user-list plan to consume listBase

Refactor docs/product/requirements/users/plan-list.md to focus on
user-specific configuration (SortMap, searchable columns, validator
rules) instead of duplicating generic listBase mechanics. Delegates
keyset cursor schema, IQueryable helpers, frontend hooks, and edge
cases to the new generic plan at docs/product/requirements/generics/
list/plan.md. Updates to consume the listBase primitives: SortMap<User>
+ SearchableColumns; GetUsersQuery subclassing KeysetQueryOptions;
GetUsersHandler calling ToKeysetPageAsync; frontend useListQuery +
ListToolbar + InfiniteTable + SortableHeader components. Verifies
Name field is now FirstName + LastName post-split.

docs: add generic list-base plan; relocate form-base plan

Move the form-base PLAN.md verbatim to
docs/product/requirements/generics/form/plan.md so it lives alongside
other generics plans and frees PLAN.md at the repo root (no longer
needed there). Add docs/product/requirements/generics/list/plan.md
describing the generic listBase primitives: backend KeysetCursor,
KeysetPage, SortMap, and IQueryable extension; frontend useListQuery,
sortable header, list toolbar, and infinite table. The user list is
the first planned consumer.

## 2026-05-14

style: add cursor-pointer to navbar sign out button

Plain <button> elements default to the arrow cursor; users expect a
pointer on interactive controls. One-class change in _protected.tsx.

## 2026-05-14

build: pin vite dev port with --strictPort

Add --strictPort to the web dev script so Vite errors out when
port 3000 is taken instead of silently drifting to the next free
port. The drift breaks better-auth: BETTER_AUTH_URL is pinned to
:3000, so a request from :3002 fails origin check on POST routes
(signOut returns 403). Failing loud on port conflict is cheaper
than debugging silent auth breakage.

## 2026-05-14

docs: note browser restart requirement after mkcert -install

Add a line to certs/README.md telling contributors to fully quit
and reopen any running browsers after running mkcert -install —
a tab reload is not enough since browsers read the trust store
on startup. Saves a "why is my cert still untrusted" round trip.

## 2026-05-14

chore: move Azure AD values to appsettings.Development.json

Relocate dev TenantId/ClientId/Audience from base appsettings.json
to appsettings.Development.json so prod inherits no Azure config
by default and has to opt in explicitly. Base appsettings.json
keeps the AzureAd block with empty strings for discoverability.
Comment out UserSecretsId in Tsz.Api.csproj since we have no
actual secrets to store yet; trivial to uncomment when needed.

## 2026-05-14

chore: configure Azure AD settings

Fill in TenantId, ClientId, and Audience in appsettings.json for
local development. These are OAuth configuration identifiers
(not credentials). Overridable via appsettings.Development.json
or environment variables per the ASP.NET configuration hierarchy.

## 2026-05-14

refactor: unify dispatcher for commands and queries

Add IRequest<TR> base interface with ICommand and IQuery extending
it. Add IRequestHandler<TRequest, TR> base handler interface; both
ICommandHandler and IQueryHandler now extend it. Dispatcher.SendAsync
accepts IRequest<TR> and looks up IRequestHandler<,> reflectively,
so both commands and queries flow through the pipeline (validation
for commands, no-op for queries). Register all handlers against
IRequestHandler<,> in DI; endpoint signatures unchanged (accept
IDispatcher now instead of IQueryHandler directly). Enables logging,
authorization, and caching behaviors across all requests uniformly.
Also fixes missed GetUserByIdHandlerTests.cs constructor call from
name-split refactor.

## 2026-05-14

feat: unify form error handling, auto-clear server errors on edit

Server-side field errors now behave like client-side ones for the
purpose of disabling Save: any unresolved error (regardless of
source) blocks submit. Drop the onServer carve-out in
hasClientSideError and rename it hasFormError. Each bound field
component (Text/Number/Select/Textarea/Checkbox/Date) clears its
own errorMap.onServer on change, so a stale server error never
sticks to a field after the user starts fixing it. The form-level
banner clears itself the moment any field value changes, via a
form.store subscription in useFormServerErrors that snapshots
values when the error is set.

## 2026-05-14

feat: sidebar shell + emerald/slate theme tokens

Replace the top-nav bar with a two-column shell: a dark slate
header (brand + greeting + sign out + theme toggle) above a
sidebar (Home, and Users for admins) and a flex main pane.
Drop the max-w-3xl wrapper on the root so the layout owns its
own width. Repoint the shadcn theme tokens from neutral grey
to emerald-primary / slate-neutrals in both light and dark.

## 2026-05-14

feat: split User.Name into firstName/lastName

Replace the single Name field on User with FirstName + LastName
across the domain entity, DTO, EF configuration, and a new
SplitUserName migration. Both fields are required and max 128
chars. UserSeeder, CreateUser, UpdateUser, and all user test
fixtures update to the new shape. Web side regenerates the
OpenAPI schema; the admin user list/create/edit routes show
and edit the two fields separately.

## 2026-05-14

feat: per-year UserLeave + bulk leaves PUT; baseform polish

- Drop the LeaveTypes module: LeaveType + LeaveAllowed fold into
  Modules/Users as supporting reference data. Catalogue (Verlof, ADV
  dagen, Anciënniteit, Ziekte) seeded via EF HasData() with stable
  GUIDs so every environment gets it on Migrate().
- UserLeave gains Year; unique index on (UserId, LeaveTypeId, Year).
  CreateUser seeds one row per LeaveType for the current year via
  injected TimeProvider. UserSeeder tops up missing rows idempotently
  so existing admins get the new shape on next startup.
- Endpoint surface for leaves shrinks to two: GET
  /api/users/{userId}/leaves?year= and atomic bulk PUT with the full
  year set. No POST/DELETE/PUT-by-id. UpdateUserLeavesValidator
  pre-loads referenced rows + joined LeaveType in one query and
  validates per-item via RuleForEach.ChildRules so failures key under
  items[i].*. Single LeavesModel migration replaces the prior
  LeavesModel+SimplifyLeaves pair.
- Admin user edit page: replace per-row dialog with a single
  useAppForm-driven Leave overview table for the current year.
  Limited rows render NumberField; Unlimited rows render a muted
  "Unlimited" label. Taken/Balance render "—" until timesheets ship.
- Baseform fixes: dirty state now reads TanStack Form's
  state.isDefaultValue so form.reset(updated) re-baselines correctly
  after save; Cancel only renders when the form is dirty (custom-
  handler cancels still always render); FormActions defaults to a
  left-aligned button row so every form is consistent.

## 2026-05-14

chore(web): prettier sweep across web package

No logical changes — re-runs of formatter on previously unformatted
files.

## 2026-05-14

feat(web): shared form base + migrate admin user forms

- useAppForm via createFormHook with bound field components (TextField,
  NumberField, SelectField, TextareaField, CheckboxField, DateField)
  and bound form components (FormActions, SubmitButton, CancelButton,
  FormErrorBanner)
- useFormServerErrors hook: ProblemDetails -> errorMap.onServer
- FormActions: Save disabled when unchanged from baseline (deep-equal);
  Cancel opt-in via `cancel` prop (true = reset, fn = custom action)
- Button: cursor-pointer / cursor-not-allowed for proper hover
  affordance
- Migrate admin users new/edit + EditLeaveDialog to the new base
- New user submit redirects to /admin/users/$id; Delete moved to header
- Update frontend-form skill for the new conventions

## 2026-05-14

refactor(api): standardise UpdateUserLeaveValidator on .WithError()

Last legacy `.WithErrorCode(CommonErrors.Invalid.Code)` chain replaced
with the typed `.WithError(CommonErrors.Invalid)` helper, so
ValidationFailure.CustomState carries the SmartEnum and the global
exception handler can read the category directly instead of falling
back to Validation. Custom message preserved via trailing .WithMessage.

Test strengthened to assert CustomState + Category.

## 2026-05-14

docs+test: post-refactor follow-ups for the result pattern

- Reflect the new dispatcher / typed-errors / ProblemDetails contract
  in agent conventions, architecture, and the backend-slice /
  unit-test / integration-test skill files.
- Close 4 coverage gaps identified by audit: AddUserLeave happy-path
  integration, UpdateUserLeave 400 ProblemDetails body shape,
  end-to-end 500 smoke via a throwing test-only endpoint, and the
  unlimited-type-with-days validator failure case. 81 unit + 34
  integration green.

## 2026-05-14

feat: dispatcher + typed errors via ProblemDetails; leaves data model

Two coupled changes landing together:

* Result pattern. In-house IDispatcher + ValidationBehavior pipeline
  replaces direct handler.HandleAsync calls. Per-module ErrorCode
  SmartEnums (UserErrors, LeaveTypeErrors, UserLeaveErrors) with
  categories (NotFound/Conflict/Forbidden/Validation) attached to
  FluentValidation failures via .WithError(). GlobalExceptionHandler
  catches ValidationException and emits RFC 7807 ProblemDetails
  (404/409/403/400 by highest-severity category; 500 generic for
  unexpected, no detail leaked). ValidationFilter removed. Frontend
  ApiRequestError parses application/problem+json; forms surface
  userMessage + per-field errors via TanStack Form errorMap.onServer.
  IUnitOfWork Begin/Close removed (unused; EF implicit txn suffices).

* Leaves model. New LeaveType (seeded reference data) + per-user
  UserLeave entity. CreateUser seeds a UserLeave row per LeaveType.
  Admin endpoints for LeaveType CRUD and per-user leave management.
  Drops User's flat HolidayDays/AdvDays/AncienniteitDays/SicknessDays
  columns.

## 2026-05-14

refactor: drop AppDbContext DbSets; route plumbing via IRepository

Extend IRepository with a per-call ignoreQueryFilters flag and a
BatchHardDeleteAsync primitive so seeders, the auth user resolver, and
integration tests can run through IUnitOfWork instead of reaching for
AppDbContext directly. With the abstraction now able to express soft-
delete bypass, the explicit DbSet<User> property comes off AppDbContext
— entities are still registered via ApplyConfigurationsFromAssembly,
and IRepository<T> resolves them through context.Set<T>().

EfCoreRepository.BatchHardDeleteAsync uses ExecuteDeleteAsync on
relational providers and falls back to load+RemoveRange+SaveChanges on
InMemory, so integration tests get the same observable behaviour
without depending on the relational SQL path.

Update the backend-module, backend-integration-test, and ef-seed
skills to reflect the new conventions: no DbSet step, tests cleanup
via BatchHardDeleteAsync, seeders take IUnitOfWork.

## 2026-05-13

feat(web): finish entra access gate UI — /no-access, admin users CRUD

Wire the access gate end-to-end on the BFF: _protected.beforeLoad now
calls getCurrentUser() after the session check and redirects null
results to a new public /no-access route. Pull the post-login nav out
of __root.tsx and into _protected.tsx so the admin link can gate
cleanly on the resolved currentUser.role (no extra fetch).

Add the admin layout + Users CRUD pages (list / new / edit-with-delete)
backed by createServerFn + TanStack Form + zod + shadcn primitives.
Role uses a styled native <select> for now.

Regen packages/web/src/api/schema.ts against the running dev API.
Replace the hand-typed shapes in users.ts with the generated
components['schemas']['UserDto'|'CreateUserCommand'|'UpdateUserCommand'|
'UserRole']. The regen also surfaced the Animals rename
(Animal→AnimalDto, *Request→*Command) plus the move to uuid ids —
animals.ts, its spec, and the $id route are realigned.

## 2026-05-13

feat: add users module and entra access gate (api-side, partial web)

Replace the per-module AnimalDbContext with a shared AppDbContext that
ApplyConfigurationsFromAssembly-discovers entity configs. Add a Users
module mirroring the Animals shape: DDD-ish entity with private
setters and static Create, soft-delete via global query filter,
filtered unique indexes on Email (per non-deleted) and EntraOid, role
stored as string, leave-default columns seeded at creation.

Introduce ICurrentUser + HttpContextCurrentUser in Tsz.Api/Common/Auth
(not Tsz.Infrastructure — the interface returns User, so pushing it
down would invert the project reference). The implementation is
scoped, memoises a single per-request DB roundtrip, matches on the
Entra oid claim with email-fallback and links EntraOid on first
login. A RequireAdmin authorization policy delegates to this same
abstraction so policy + handlers see one source of truth for role.
DefaultMapInboundClaims is disabled so oid/sub/email arrive
unmolested.

Endpoints: GET /api/users/me (JWT only, 404 when unprovisioned) plus
admin-gated CRUD on /api/users with soft delete. JsonStringEnumConverter
makes role serialise as a string union over the wire. Initial EF
migration creates both tables; a dev-only UserSeeder writes one Admin
row idempotently.

Tests: 39 unit (handlers, validators, UnitOfWork now on AppDbContext)
and 23 integration (in-memory DB per fixture, IAsyncLifetime wipes
users between tests, covers /me 404 + oid match + email→oid link,
RequireAdmin 403 paths, dup-email 409, soft-delete hidden by query
filter).

Web side adds a hand-typed users.ts API wrapper and a current-user
server fn — schema regen still pending (see
docs/product/requirements/users/IMPLEMENTATION_STATUS.md for the
handoff incl. route/UI work still to do).

## 2026-05-12

docs: inline AGENTS.md content into CLAUDE.md and stress subagents

Previously CLAUDE.md was a single-line pointer to AGENTS.md. Inline
the full agent guidance and add an `(important!)` emphasis on
spawning subagents to keep context lean.

docs: align login plan and ui-plan with current auth implementation

Update plan.md and ui-plan.md to match the implementation that
actually shipped: SQLite-backed sessions instead of stateless cookies,
`BETTER_AUTH_SECRET`/`BETTER_AUTH_URL` env var names, the rolled-back
`__Host-` prefix and `SameSite=Strict` (now `Lax` defaults), the
`scope` (singular) vs `scopes` field-name pitfall and
`disableDefaultScope: true` requirement, the dev-only JwtBearer debug
hooks on the API, and the auto-redirect from RootLayout instead of a
manual Login button. Delete the obsolete login-database.plan.md — the
migration it described is complete.

fix: correct scope field name so entra returns api access token

Better Auth's Microsoft social provider reads `scope` (singular), not
`scopes`. The misnamed field meant the configured
`api://.../access` scope was silently dropped during sign-in, so
Microsoft returned a Graph access token instead of one for the API
and signature validation failed. Rename the field, disable the
hardcoded `User.Read` default to keep the request scoped to a single
resource, and source the API client id from `API_CLIENT_ID`.

Add dev-only JwtBearer debug events on the API to surface inner
exceptions and decoded claims when validation fails. Pre-fill the
public Entra Instance URL in appsettings so only secrets stay in
user-secrets.

fix: persist sessions to bun:sqlite and relax cookie hardening for OAuth

Switch Better Auth's database from memoryAdapter to a bun:sqlite-backed
file (auth.db) so sessions survive SSR HMR restarts instead of being
wiped on every reload. Add a Microsoft profile mapper so the BetterAuth
user id is the Entra `oid` (falling back to `sub`). Comment out the
`__Host-timesheetzone` cookie prefix and drop SameSite=Strict on
session_token: the post-OAuth redirect chain (microsoft.com → /callback
→ /) is cross-site per the SameSite spec, so a Strict cookie is dropped
on the first / request and the route guard sees a null session. Lax
(BA default) survives the redirect; __Host- is also incompatible with
BA's path-scoped OAuth state cookies. Ship a bun:sqlite type shim so
the Bun built-in module typechecks.

feat: serve web and api over local HTTPS for OAuth dev

Wire up mkcert-generated certs in Vite's dev server and switch
BETTER_AUTH_URL and API_URL to https. Enable HttpsRedirection in the
.NET API and run dotnet watch with the https launch profile. Add a
check:certs preflight in the web dev script and ignore the cert files
(keeping certs/README.md tracked). Includes WIP debug logs in
getSession and __root to investigate a session-null-after-OAuth bug.

fix: scope SameSite=Strict to session cookie only to resolve state_mismatch

State and PKCE cookies need SameSite=Lax (Better Auth's default) to
survive the cross-site redirect from Microsoft during OAuth. Applying
Strict globally stripped those cookies on the redirect, causing a
state_mismatch error. Add inline comments explaining the rationale and
update plan and ui-plan docs accordingly.

feat: harden session cookies with __Host- prefix and Strict SameSite

Apply __Host-timesheetzone cookie prefix and SameSite=Strict, Secure,
HttpOnly, Path=/ to all betterAuth session cookies. The __Host- prefix
prevents subdomain cookie injection; Strict SameSite blocks cross-site
request inclusion. Documents the requirements in the login plan.

## 2026-05-11

feat: add Microsoft Entra authentication via Better Auth

Set up Better Auth with the Microsoft social provider for Entra
ID SSO. Adds protected route layout, auth API handler, session
management with tanstackStartCookies plugin, and initialises the
in-memory adapter with the required model tables. Removes old
unprotected routes and replaces the app entry point with a
redirect to the protected section.

## 2026-05-11

docs: split product requirements into subfolders and add login plan

chore: reformat changelog to date-based format and update commit skill

style: remove commented-out alias block from vite config
chore: replace vite alias with tsconfig baseUrl for path resolution
docs: add product requirements, architecture, and agent convention docs
chore: consolidate vitest config into vite config and add @tests alias
chore: update test to use new fetchutils
feat: extract api client module, add animals api tests, and fix age type errors
chore: add Ref and Exa MCP servers and commit skill changelog step
test: add unit tests for ValidationFilter
chore: add AGENTS.md with monorepo overview and conventions pointers
feat: fix OpenAPI schema to emit strict number types and required fields
feat: add validate skill, search filter feature, and test infrastructure

## 2026-05-08

chore: add claude skills directory with implement, plan, validate, and skill-creator skills
docs: rewrite commit skill with split, secret, push rules
chore: move claude.md to repo root as CLAUDE.md
chore: add dbhub MCP server config for animals.db
chore: add commit slash command and skill
Enhance REVIEW.md with strict OpenAPI specifications and required field handling for improved TypeScript type generation
Update REVIEW.md to include strict OpenAPI specs for improved TypeScript type generation
Add onSubmit handler to save animal data in REVIEW.md
Add REVIEW.md for guidelines and improvements

## 2026-05-07

Retrofit animals routes with shadcn UI primitives
Validate animal server fn inputs with zod
Add edit form on animal detail and route fetches via Start server
Revert animals loaders to direct fetch without createServerFn
Ignore Visual Studio .vs folder
Restructure animals routes and pin fetches to server
Reformat OpenAPI schema with single quotes
updated settings.json schema
Add startup database seeding for animals
Initial commit

## 2026-05-20

refactor: extract soft-delete paging into ISoftDeletable seam

Add ISoftDeletable interface (DateTimeOffset? DeletedAt) for domain
entities. Implement soft-delete-aware GetPagedAsync extension that
auto-applies the DeletedOnly filter and ignoreQueryFilters toggle.

Handler boilerplate drops from 8 lines to 1 call. Soft-delete
semantics concentrate in one place, testable as a contract.
Tests pass unchanged (159 unit, 61 integration).
