# ADR-0004: Impersonation — capability-transparency over traceability

## Status

Accepted — 2026-05-27

## Context

Impersonation (`docs/product/requirements/login/impersonation/plan.md`) lets an Admin assume another User's identity. Several alternatives were weighed against a v1 internal-tool bar.

## Decision

- **Replace-not-union identity.** While impersonating, the effective identity *replaces* the Admin's; the Impersonator can do exactly what the Impersonated User could — no Admin-only powers carry over. The only capabilities reserved to the real Admin are stop/swap impersonation (BFF-local, never API-authorized).
- **Admin-only initiation, validated server-side.** The `X-Impersonate-User` header is a directive, not a grant: the API honors it only when the JWT's DB user holds `Admin`. A signed BFF cookie carries the active target.
- **No Admin targets.** An Admin may not impersonate a User holding the `Admin` role — the one path with no legitimate use beyond acting untraceably at admin level.
- **No trace (deliberate).** Actions performed while impersonating — including `TimesheetWeek` Submit/Approve/Reopen — are attributed solely to the Impersonated User, with no persisted record of the Impersonator. We chose capability-transparency over attestation-traceability. No actor field, no audit table, no per-action stamp.

## Consequences

- A billing-grade attestation (timesheet approval) can be performed under impersonation indistinguishably from the named user doing it. Accepted for v1 on a trusted-admin internal tool.
- Retrofitting traceability later means a schema change *and* loss of historical trace — this decision cannot be applied retroactively. Revisit (a `StatusChangedUnderImpersonationBy` stamp, then a full audit log) if accountability requirements arrive.
