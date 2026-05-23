# ADR-0002: Promote a sub-concept to its own module when it crosses a module boundary

## Status

Accepted — 2026-05-23

## Context

`LeaveType` was originally its own module (`Tsz.Api/Modules/LeaveTypes/`, commit 760488c, 2026-05-14). The 2026-05-20 refactor (`983df08`, "extract domain modules to separate assemblies") absorbed it into `Tsz.Modules.Users` for *packaging pragmatism* — at 4–5 files it was too small to justify its own assembly pair while `UserLeave` (per-user allowance) sat right next to it.

That reasoning held while `LeaveType` had a single consumer inside the Users module (`UserLeave`). Adding the `Timesheets` module — which references `LeaveTypeId` on `LeaveBooking` — introduces a second consumer outside Users. Exposing `LeaveType` queries through `IUsersAccessModule` would mean `Users.Contracts` advertises operations that aren't semantically about users.

## Decision

Extract `LeaveTypes` into its own module pair (`Tsz.Modules.LeaveTypes` + `Tsz.Modules.LeaveTypes.Contracts`) before the Timesheets module is built.

Generalise the rule: **when a domain concept living inside a host module gains a second consumer outside that host, promote it to its own module.** Cross-module visibility is the criterion, not size. A "tiny but standalone" module is acceptable when its public surface needs an honest name.

`UserLeave` stays inside Users for now — it has only one consumer (the User admin form) and is intrinsically about a User.

## Considered alternatives

- **Keep `LeaveType` in Users and expose via `IUsersAccessModule`.** Cheapest. Rejected: `Users.Contracts` would advertise queries that aren't about users, degrading the semantic cohesion of the Users facade and making future readers wonder why leave-type queries live there.
- **Move `LeaveType` into the new `Timesheets` module.** Rejected: creates a Users → Timesheets dependency cycle through `UserLeave.LeaveTypeId`. Forbidden DAG.

## Consequences

- One additional module pair to maintain. Justified by clean public surfaces and a cycle-free dependency graph.
- Partially reverses `983df08`'s packaging-driven absorption of `LeaveType`. The "merge tiny modules" reasoning from that refactor applies only when the merged concept has a *single* consumer.
- Establishes a generally applicable rule for future similar situations. If any other host-module sub-concept gains a second cross-module consumer, the same extraction is expected.

## Trigger to revisit

This ADR is a rule, not a one-off. Revisit only if the rule itself proves wrong — e.g. if the cost of extraction (assembly pairs, public surface design, migrations) consistently exceeds the cost of degraded host-module cohesion. In that case, propose a new ADR amending or replacing this one.
