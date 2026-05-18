# ADR-0001: Functional decomposition for domain modules

## Status

Accepted — 2026-05-18

## Context

We have eight requirement areas (login, users, customers, contracts, leave types, leave overview, time entry, timesheets) and two backend modules so far (`Auth`, `Users`). We need to decide how to grow the module structure.

Two options were considered:

1. **Functional decomposition** — one module per requirement subject. Direct, familiar, easy to map docs → code.
2. **Volatility-based decomposition** (Juval Löwy / IDesign) — modules organised around axes of change, layered as Clients / Managers / Engines / Resource Access / Resources, with Utilities cross-cutting.

The team is not practiced in volatility-based decomposition. Löwy himself notes it's a skill that takes years to acquire, and a mis-applied volatility decomposition produces *worse* coupling than functional, because the abstractions have committed to change axes that turn out not to exist.

## Decision

Go functional for v1. One module per requirement subject, broadly mirroring `docs/product/requirements/`. `Auth` stays as cross-cutting infrastructure rather than a domain module.

Maintain `docs/product/volatilities.md` as a watch-list of the change axes we expect, plus a sketch of what a volatility-based decomposition *would* look like, so:

- We recognise volatility hotspots when they fire (instead of papering over them).
- A future refactor toward volatility-based modules doesn't start from a blank page.

## Alternatives considered

- **Volatility-based now.** Rejected. Risk of guessing the wrong axes outweighs the cost of cross-module ripples under functional. See `volatilities.md` for the candidate decomposition we'd reach for if we changed our minds.
- **Single monolithic module.** Rejected. We already have too many distinct concepts; the vertical-slice handler pattern needs modules to slice.
- **Hybrid — functional modules with a few pre-extracted engines (e.g. `LeaveRulesEngine`).** Tempting but rejected for v1: extracting engines before we've felt the rules change is the same speculation problem in a smaller costume. Revisit per `volatilities.md`'s early-warning signals.

## Consequences

Positive:
- Direct mapping requirements → modules; low onboarding cost.
- Aligns with the existing vertical-slice convention (`Modules/<Name>/Features/<Op>.cs`).
- Easy to delete or rename a module if a requirement is dropped.

Negative:
- Cross-module ripples are expected, especially for leave rules, workday semantics, and time-entry workflow (see `volatilities.md`).
- We need discipline to revisit `volatilities.md` when ripples occur, rather than absorbing them silently.

## Trigger to revisit

Any of the early-warning signals listed in `volatilities.md` — e.g. one PR routinely touches `Users` + `LeaveTypes` + `LeaveOverview` + `TimeEntry` together for the same business reason. At that point, open a follow-up ADR proposing the specific extraction.
