# Volatilities

This is a watch-list, not a design. We ship with functional decomposition (see [ADR-0001](../adr/0001-decomposition-approach.md)); this doc records the axes of change we *expect* so we recognise them when they fire, and so a future volatility-based refactor doesn't start from a blank page.

For each requirement: what is stable, what we expect to change, and which IDesign layer the change would live in (Client / Manager / Engine / Resource Access / Resource / Utility).

---

## Per-requirement volatility analysis

### Login
- **Stable:** that authentication exists; one app registration shared by web + API.
- **Volatile:**
  - Identity provider — Entra today; single-tenant. Multi-tenant or alternative providers (local accounts, Google) are plausible.
  - Role taxonomy — Admin / User / Client Manager today; HR / Finance / Manager-hierarchy plausible.
  - Impersonation — listed as optional; will arrive or not based on support needs.
- **Layer:** Utility (auth abstraction) + Resource (Entra).

### Users
- **Stable:** an employee has a name, email, role.
- **Volatile:**
  - Per-user leave configuration (which types, totals, limits).
  - New-user prefill rules ("20 leave / 5 ADV / 0 ancientiteit / 0 sickness" is HR policy, not code).
  - Working pattern (part-time, FTE) — implied by time entry but not modelled yet.
  - Role taxonomy (shared with Login).
- **Layer:** identity → Resource Access; leave config & prefill rules → Engine (`LeaveRulesEngine`).

### Customers
- **Stable:** customer record with address + contact.
- **Volatile (mild):**
  - Address structure if scope leaves Belgium.
  - Client Manager assignment (Prio 2).
  - Billing / VAT details, plausible later.
- **Layer:** Resource Access. Boring CRUD, low volatility — the *behavior* attached to customers lives elsewhere.

### Contracts
- **Stable:** customer ↔ contract ↔ consultant, date-bounded, with tasks.
- **Volatile:**
  - Task/rate model (single rate today; tiered rates, VAT, multi-currency plausible).
  - "Selectable in time entry" rules (today: date range; plausibly: status, approval, hours budget).
  - Client Manager assignment (Prio 2).
- **Layer:** storage → Resource Access; selection rules → Engine (`ContractSelectionEngine`).

### Leave Types
- **Stable:** there is a configurable list.
- **Volatile (HIGH):**
  - Semantics of Allowed / NotAllowed / Limited.
  - Default totals.
  - Accrual rules: annual reset, carryover, proration on start date, part-time scaling.
  - "Ancientiteit" — legislation/HR-policy driven, will evolve.
  - Per-country variation if scope grows beyond Belgium.
- **Layer:** rule logic → Engine (`LeaveRulesEngine`); definitions → Resource Access.

### Leave Overview
- **Stable:** year-grid concept, summary of balance.
- **Volatile:**
  - Holiday data source (openholidaysapi today; could cache, augment with company closures).
  - Visual rules (half-day, current-day, weekend, school vs work holidays).
  - Interaction (Prio 2: click a day → deep link into time entry).
- **Layer:** grid → Client; balance & holiday computation → Engine (shared with Time Entry); holiday source → Resource.

### Time Entry
- **Stable:** time is entered per week, per task or leave row.
- **Volatile (HIGH):**
  - Input semantics: `00:15`–`8:00` range, `d`/`h`/`del` hotkeys, what counts as a valid value.
  - What is editable — weekend & holiday exclusion varies by country and possibly contract type.
  - Approval workflow — today: "submit for approval" flips a flag. Plausibly: multi-step approvers, rejection, comments, re-open.
  - Auto-save semantics on navigation.
  - Currently out-of-scope (km, comments) — likely to return.
- **Layer:** workflow → Manager (`TimeEntryManager`); validation rules → Engine; grid → Client.

### Timesheets
- **Stable:** month aggregation of time entries.
- **Volatile:**
  - Aggregation dimensions (per task / per customer / per consultant / per leave type).
  - Approval display rules (light → dark green today; could grow states).
  - Export — none today; payroll / PDF / Excel are plausible adds.
- **Layer:** aggregation → Manager (`TimesheetManager`) + Engine (`TimesheetAggregator`); future export → its own Client.

---

## Synthesis — shared volatilities

The same axis of change runs through multiple requirements. These are the candidate seams:

| Volatility | Touches | Candidate module (layer) |
|---|---|---|
| Leave rules (accrual, limits, balance, ancientiteit) | Users, Leave Types, Leave Overview, Time Entry | `LeaveRulesEngine` (Engine) |
| Workday / calendar semantics (weekend, holiday, working pattern) | Time Entry, Leave Overview, Timesheets, Contracts | `WorkdayEngine` (Engine) |
| Time-entry workflow (submit, approve, auto-save) | Time Entry, Timesheets, future Payroll | `TimeEntryManager` (Manager) |
| Aggregation & reporting | Timesheets, Leave Overview, future Payroll | `TimesheetAggregator` (Engine) + `TimesheetManager` (Manager) |
| Identity & roles | Login, Users, every authorised endpoint | `Auth` (Utility) |
| Consumer-specific UI | Admin vs Employee vs future Payroll / Mobile | distinct Clients |
| Master-data persistence | Customers, Contracts, Users, Leave Types | Resource Access per aggregate |

## Candidate volatility-based decomposition (for reference, NOT what we're building)

```
Clients:
  AdminWeb        — Customers, Contracts, Users, LeaveTypes admin
  EmployeeWeb     — TimeEntry, Timesheets, LeaveOverview
  [future] PayrollExport
  [future] Mobile

Managers (workflow volatility):
  TimeEntryManager        — week submission, approval lifecycle, auto-save
  TimesheetManager        — month aggregation, export
  UserOnboardingManager   — create user + prefill leaves + assign role

Engines (rule volatility):
  LeaveRulesEngine        — accrual, limits, balance, ancientiteit
  WorkdayEngine           — weekend/holiday/working-pattern, editability
  ContractSelectionEngine — which contracts/tasks are valid for a given week
  TimesheetAggregator     — per-task / per-customer / per-leave rollups

ResourceAccess (persistence volatility):
  UserAccess, CustomerAccess, ContractAccess, TimeEntryAccess, LeaveAccess

Resources:
  AppDb, EntraID, OpenHolidaysAPI

Utilities:
  Auth, Logging, Validation
```

What disappears versus the functional view:
- **No `Login` module** — authentication is a Utility, not a domain.
- **No `Customers` module as a domain unit** — it's a Resource Access table; behavior attached to customers lives in `ContractSelectionEngine` and `TimesheetAggregator`.
- **No `Leave Overview` module** — it's a Client view composed from `LeaveRulesEngine` + `WorkdayEngine` + `LeaveAccess`.
- **`Time Entry` is split four ways** — workflow (Manager), rules (Engine), storage (Resource Access), UI (Client). Today they sit together; that grouping is the most likely future regret.

---

## Early-warning signals

Extract a volatility-based module when one of these starts happening:

- HR changes leave rules and one PR touches `Users` + `LeaveTypes` + `LeaveOverview` + `TimeEntry` → extract `LeaveRulesEngine`.
- A new country / locale means edits across `TimeEntry` + `LeaveOverview` + `Contracts` + `Timesheets` → extract `WorkdayEngine`.
- A second consumer of time entries appears (mobile, payroll export, external API) and the time-entry workflow is trapped inside the HTTP / UI layer → extract `TimeEntryManager`.
- Approval grows beyond a single boolean (multi-step, comments, rejection) → that's `TimeEntryManager` arriving on its own.
- Reporting needs cross slices (per customer + per consultant + per leave type) and the queries duplicate across endpoints → extract `TimesheetAggregator`.
