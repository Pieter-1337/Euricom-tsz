# ADR-0003: Domain invariants live in the aggregate; cross-module and cross-aggregate concerns live in the validator

## Status

Accepted — 2026-05-24

## Context

The `Tsz.Modules.Timesheets` module has grown two distinct kinds of rule:

- **Aggregate-resident rules** (`TimesheetWeek` today): structural uniqueness of children by `(ContractTaskId, Date)` / `(LeaveTypeId, Date)`, `EnsureDraft` lifecycle guard, `Submit` / `Approve` / `Reopen` state-machine transitions.
- **Validator-resident rules** (`ApplyTimesheetWeekBookingsValidator` today): per-entry duration range, date-inside-week, business-day check (via `IWorkdaysAccessModule`), eligible-task check (via `IContractsAccessModule`), leave-type-exists check (via `ILeaveTypesAccessModule`), yearly leave allowance (spans multiple `TimesheetWeek`s + `UserLeave`).

The pattern that emerged wasn't designed up-front — it formed by accretion. When grilling the day-capacity rule (Σ `TimeEntry` + `LeaveBooking` `DurationHours` per `(User, Date)` ≤ `WorkdayCapacity`), placement wasn't obvious: the day-cap *looks* like a numeric business rule that the validator handles, but it differs from every existing validator rule in one specific way — it depends on **nothing outside a single `TimesheetWeek`**.

That difference is the load-bearing distinction. Making it explicit lets future rule placements be mechanical rather than re-litigated each time.

## Decision

A rule's home is determined by what data it needs to evaluate:

- **An invariant that depends only on a single aggregate's own state lives in the aggregate.** The aggregate enforces it inside the mutator that could violate it; if the input would produce an invalid post-state, the mutator throws `ValidationException` carrying the relevant `TimesheetErrors` code.
- **A check that requires cross-module data, or spans multiple aggregates, lives in the validator.** The validator pulls the external data via module facades (`IContractsAccessModule`, `IWorkdaysAccessModule`, `ILeaveTypesAccessModule`, …) or queries sibling aggregates via the repository, and emits a `ValidationFailure` with the relevant error code.

Concretely:

| Rule | Data scope | Home |
|---|---|---|
| `EnsureDraft` / state-machine transitions | single `TimesheetWeek.Status` | Aggregate |
| Child uniqueness `(ContractTaskId, Date)` etc. | single `TimesheetWeek.Entries` | Aggregate |
| **Day-capacity** (Σ children per Date ≤ `WorkdayCapacity`) | single `TimesheetWeek` post-state | **Aggregate** (first application of this ADR) |
| Date inside week | command vs `IsoYear`/`IsoWeek` | Either is honest; validator chosen because it composes with the cross-module date checks below |
| Business-day check | `IWorkdaysAccessModule` | Validator |
| Eligible-task check | `IContractsAccessModule` | Validator |
| Leave-type exists | `ILeaveTypesAccessModule` | Validator |
| Yearly leave allowance | sibling `TimesheetWeek`s + `UserLeave` (via `ILeaveTypesAccessModule`) | Validator |

The day-capacity rule is the first deliberate application of this ADR. It produces:

- A single `TimesheetWeek.ApplyBookings(timeEntries, leaveBookings)` mutator (folding the previously separate `ApplyTimeEntries` / `ApplyLeaveBookings`, which could not jointly enforce the invariant).
- A new error code `ERR_TIMESHEET_DAY_CAPACITY_EXCEEDED` in `TimesheetErrors`.
- No validator change for this specific rule (defence-in-depth duplication explicitly rejected — see Alternatives).

## Alternatives considered

- **Validator-only for the day-capacity rule.** Cheapest — matches the most populous slot in the existing pattern table. Rejected on inspection: every other validator rule needs data the aggregate can't see (cross-module facades, other aggregates). The day-cap doesn't. Placing it in the validator by reflex would have pushed a pure aggregate invariant out of the aggregate, weakening the structural guarantee that an in-memory `TimesheetWeek` is always self-consistent.
- **Both validator + aggregate (belt-and-braces).** Rejected: the validator can't produce information the aggregate doesn't already have — both run in the same request against the same payload. Double-enforcement adds maintenance cost (two places to update if `WorkdayCapacity` ever becomes per-user) with no observable benefit.
- **Move *all* numeric rules to the aggregate** (including per-entry duration range, currently in the validator). Tempting, but out of scope for this ADR. The per-entry duration check is a real migration candidate — flagged under Consequences below — but doing it now would conflate the placement rule with a refactor.

## Consequences

Positive:

- **Source-of-truth clarity.** If a `TimesheetWeek` instance exists in memory and accepted its last mutation, its single-aggregate invariants are guaranteed. Callers don't need to know which validator ran before them.
- **Aggregate boundary stays load-bearing.** ADR-0001's volatilities.md predicted `TimesheetWeek` as a high-volatility hotspot for booking semantics. Keeping its own invariants inside it means future booking-rule changes (e.g. minimum total per workday, overtime cap) have one canonical home.
- **Validators stay focused on what they uniquely can do** — composing data from other modules and other aggregates.

Negative:

- The aggregate gets thicker as more single-aggregate rules land. This is the intended trade-off but should be revisited if `TimesheetWeek` starts to outgrow the readable-in-one-screen heuristic.
- **One migration candidate flagged, not done:** per-entry duration validity (`0.25 ≤ d ≤ 8.00`, multiples of 0.25) is currently in the validator. By this ADR's rule it belongs in `TimeEntry.Create` / `TimeEntry.Update` and `LeaveBooking.Create` / `LeaveBooking.Update`. Migration is left for a separate slice when the area is next touched — explicitly *not* a precondition for the day-capacity slice.
- Tests that previously asserted validator behaviour for an aggregate-resident rule must move to aggregate-level tests (`Tsz.Modules.Timesheets.Tests` against `TimesheetWeek` directly, not against the validator).

## Trigger to revisit

This ADR is a rule, not a one-off. Revisit only if the rule itself proves wrong — e.g.:

- A future invariant that depends only on aggregate state turns out to be hostile to in-aggregate enforcement (large data load, performance) and is better expressed at the validator boundary even at the cost of the structural guarantee.
- A rule arises that depends on **both** aggregate state and cross-module data and resists clean splitting. Likely resolution: split the rule — aggregate handles the in-aggregate half, validator handles the cross-module half, both fail with codes the FE can disambiguate.

If either pattern recurs, open a follow-up ADR amending the rule.
