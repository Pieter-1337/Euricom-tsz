> _Drafted by an AI agent (`/matt-to-prd`) from the impersonation plan (`docs/product/requirements/login/impersonation/plan.md`) and ADR-0004. Sharpen via comments/edits before `/matt-to-issues` splits it._

## Problem Statement

When a consultant reports a problem with their timesheet — a week that won't submit, leave that looks wrong, a screen that behaves unexpectedly — an Admin currently has no way to see the app *as that user sees it*. They can query the database or read code, but they cannot reproduce the user's exact view (their sidebar, their data scope, their permission blocks) or act on the user's behalf (submit or approve a `TimesheetWeek` for them when the user is unavailable). Support and debugging are slow and indirect, and "act on behalf of" is impossible without sharing credentials.

## Solution

Let an **Admin** **impersonate** another User — assume that User's identity for the session, seeing and doing **exactly** what the **Impersonated User** could, no more and no less. Impersonation is a request-scoped **effective identity** swap at the app layer: it never mints or swaps the real Entra access token. The real Admin (the **Impersonator**) stays signed in with their own token; the app simply resolves an *effective* user for authorization, data scope, and writes.

While impersonating, the Impersonator:
- sees the Impersonated User's sidebar, data, and `/me` — a faithful "view as user";
- is blocked from Admin-only routes (the effective user is not Admin) — correct behavior, not a bug;
- can perform any write the Impersonated User could, including `TimesheetWeek` Submit/Approve/Reopen (read-write, no attestation carve-out);
- always sees their **own** real name in the header greeting ("Hi, {admin}") plus a red banner naming who they are impersonating, with a Stop control.

Only a real Admin may initiate impersonation, and Admins may **not** impersonate other Admins. Actions performed under impersonation are attributed **solely** to the Impersonated User with **no persisted trace** of the Impersonator (capability-transparency over traceability — ADR-0004); the only signal is one transient log line per request.

## User Stories

1. As an Admin, I want to impersonate a consultant, so that I can see the app exactly as they see it while debugging their reported problem.
2. As an Admin, I want the impersonated view to show the consultant's sidebar (collapsed to their nav, no Users/Customers), so that I can confirm what they can and cannot reach.
3. As an Admin, I want the impersonated view to return the consultant's data on every scoped screen, so that I reproduce their exact state rather than my own.
4. As an Admin, I want `/me` to return the impersonated user while I'm impersonating, so that profile-driven UI reflects the target, not me.
5. As an Admin, I want to be blocked from Admin-only routes while impersonating a plain user, so that the "view as user" fidelity is honest — I see their blocks too.
6. As an Admin, I want to submit a consultant's Draft `TimesheetWeek` on their behalf, so that I can unblock them when they are unavailable.
7. As an Admin, I want to approve or reopen a `TimesheetWeek` on behalf of a user whose role permits it, so that act-on-behalf-of is fully read-write, not read-only.
8. As an Admin, I want my own real name to remain in the header greeting while impersonating, so that I never lose track of who I really am.
9. As an Admin, I want a persistent red banner naming the user I'm impersonating, so that I have an unmistakable, always-visible reminder that I am not myself.
10. As an Admin, I want a Stop control in the banner, so that I can end impersonation and return to my full admin view in one click.
11. As an Admin, I want an Impersonate button in the header (visible only when I am Admin and not already impersonating), so that I can start impersonation from anywhere in the app.
12. As an Admin, I want a searchable picker of impersonation targets, so that I can quickly find the user I need by name or email.
13. As an Admin, I want the picker to exclude other Admins, so that I am never offered an invalid target.
14. As an Admin, I want a per-row Impersonate action in the Users list, so that I can start impersonation directly from where I'm already looking at a user.
15. As an Admin, I want re-targeting to just switch users without a stop-first step, so that moving between users is frictionless.
16. As an Admin, I want impersonation to end automatically when I sign out, so that it never outlives my session.
17. As a security-conscious engineer, I want the `X-Impersonate-User` header treated as a directive and re-validated server-side on every request, so that the header can never act as a grant.
18. As a security-conscious engineer, I want a non-Admin sending the impersonation header to be rejected with 403, so that the header cannot escalate privileges.
19. As a security-conscious engineer, I want a header naming a non-existent user to fail with 404 and a malformed value to fail with 400, so that bad directives fail loud rather than silently falling back to the caller's identity.
20. As a security-conscious engineer, I want a header naming an Admin target rejected with 403, so that no one can act untraceably at admin level.
21. As a security-conscious engineer, I want the request to never silently fall back to the caller's own identity when a directive is invalid, so that an Admin can never write as themselves while believing they act as the target.
22. As a security-conscious engineer, I want the BFF impersonation cookie signed and session-bound (`impersonatorId === current session user id`), so that a stale or stolen cookie cannot be reused across sessions.
23. As a security-conscious engineer, I want the cookie hardened (`__Host-` prefix, `Secure`, `HttpOnly`, `SameSite=Strict`), so that it is protected beyond the OAuth-constrained session cookie.
24. As an Admin, I want impersonation re-validated every request, so that if the target is deleted or promoted to Admin mid-session, impersonation immediately stops being honored.
25. As an operator, I want a single structured log line when impersonation is engaged for a request (real oid + target id), so that I have transient diagnostics without a durable audit trail.
26. As an existing authorization consumer (RequireAdmin, RequireAdminOrSelf, RequireAdminOrAnyClientManager, DataScopeAccessor), I want to keep using `ICurrentUserResolver` unchanged and transparently receive the effective user, so that the swap is contained to one seam and no consumer code changes.
27. As an Admin using `RequireAdminOrSelf` endpoints while impersonating, I want "self" to resolve to the effective (impersonated) user, so that I can reach the target's own resources but not my own under their identity.
28. As a non-impersonating user, I want behavior to be byte-for-byte identical to today when no header is present, so that impersonation introduces zero regression to the normal path.

## Implementation Decisions

**Identity model (ADR-0004).** Replace-not-union: the effective identity *replaces* the Impersonator's; no Admin-only powers carry over. Read-write (act-on-behalf-of), no attestation carve-out for `TimesheetWeek` transitions. No persisted trace of the Impersonator — no actor field, no audit table, no per-action stamp; one transient `ILogger` line only. Admin-only initiation; no Admin targets.

**Modules to build/modify:**

1. **Dual-identity resolution (deep module).** Introduce `IImpersonationContext` (request-scoped: `Guid? TargetUserId`, `IsImpersonating`, internal `SetTarget`) and `IRealUserResolver` (resolves the *real* caller strictly from JWT claims → DB `User`, keeping the email→oid auto-link; impersonation-unaware). Split the existing `CurrentUserResolver`: extract claim-based logic into `RealUserResolver : IRealUserResolver`; rewrite `CurrentUserResolver` as the **effective** wrapper over `IRealUserResolver` + `IImpersonationContext`, loading the target `User` by id when impersonating, else the real user, caching per request. The `ICurrentUserResolver` / `ICurrentUserAccount` public surface stays identical so no authz consumer changes. Interfaces live in `Tsz.Infrastructure/Auth` (so `Tsz.Api` middleware can depend on them); impls in the Users module where the `User` aggregate lives.

2. **ImpersonationMiddleware (trust-boundary gate).** `IMiddleware` registered between `UseAuthentication()` and `UseAuthorization()`. When `X-Impersonate-User` is present: resolve the real user via `IRealUserResolver`; if null or not Admin → 403; parse target Guid (malformed → 400); load target `User` (missing → 404); if target holds Admin → 403; on success `SetTarget(targetId)` and emit one structured log line. Absent header → no-op (identical to today). The header is a directive, never a grant; the gate is the hard guard regardless of any BFF-side checks. Shared constant `ImpersonationHeader.Name = "X-Impersonate-User"`.

   Honored-impersonation condition:
   ```
   honored ⇔ valid JWT
           ∧ DB user of JWT subject (oid) has role Admin
           ∧ target userId exists
           ∧ target user does NOT hold the Admin role
   ```
   Status codes: real caller not Admin → 403; target missing → 404; target holds Admin → 403; malformed guid → 400.

3. **GetImpersonationTargets query.** New query + handler returning **non-Admin** users with a `search` filter, reusing the existing `KeysetQueryOptions` / `SearchableField` machinery (cf. `GetUsersPaged`) plus a base predicate excluding users whose `RoleAssignments` contain Admin. Mapped as `GET /api/users/impersonation-targets?search=&...` under `RequireAdmin` (only a real, non-impersonating Admin calls it). Keeps the picker from offering invalid targets; the middleware remains the hard guard.

4. **BFF impersonation server fns (`impersonation.server.ts`).** Signed, session-bound `__Host-tsz_impersonate` cookie. Payload `{ impersonatorId, targetId, targetName }`, HMAC-SHA256 over payload keyed by `BETTER_AUTH_SECRET`. `startImpersonation({ targetUserId, targetName })` sets the cookie from the current betterAuth session; `stopImpersonation()` clears it; `getImpersonation()` verifies signature **and** `impersonatorId === current session user id`, returns `{ targetId, targetName } | null`. `bearerMiddleware` runs the same verify and emits `X-Impersonate-User: <targetId>` **only when** signature is valid and `impersonatorId` matches the live session (else omit the header and ideally clear the stale cookie). `signOut()` clears the cookie.

   Cookie hardening (beyond the session cookie, which OAuth forces to relax): `__Host-` prefix (`Path=/`, no Domain, Secure), `HttpOnly`, `SameSite=Strict`, signed, session-lifetime, cleared on stop + signOut.

5. **Impersonation UI.** In `_protected.tsx`, an **identity split**: header greeting uses the **real** session `user`; sidebar nav gating and all page data use the **effective** `currentUser` (unchanged). Header inline **Impersonate** button between greeting and Sign out, shown only when `currentUser` is Admin **and** not impersonating; opens the picker. Full-width **red banner** beneath the header when `getImpersonation()` is non-null: "You are impersonating **{targetName}**" + a **Stop** control. `impersonate-dialog.tsx`: shadcn `Dialog` + `Command` searchable picker hitting `GET /api/users/impersonation-targets`; selecting a row calls `startImpersonation` then reloads to `/`. Second entry point: per-row **Impersonate** action on `/admin/users` (admin-only). No new top-level route, so no new sidebar NavLink. shadcn + destructive/red styling per `DESIGN.md`.

**Pipeline & DI.** Register `IImpersonationContext → ImpersonationContext` (scoped), `ImpersonationMiddleware`, `RealUserResolver` (`IRealUserResolver`) and the effective `CurrentUserResolver` (`ICurrentUserResolver` + `ICurrentUserAccount`) — all scoped — in `UsersModule` / `Program.cs`. Insert `UseMiddleware<ImpersonationMiddleware>()` between `UseAuthentication()` and `UseAuthorization()`.

**API client.** Regenerate the frontend schema (`bun --filter web gen:api`) to pick up the new targets endpoint.

**No new C# project** — all changes land in existing `Tsz.Infrastructure`, `Tsz.Api`, `Tsz.Modules.Users`; no `tsz.slnx` change (confirm during impl).

**Token & scope untouched.** `disableDefaultScope` / token audience unchanged. The real Entra token (the Admin's) is still forwarded as Bearer; no token is ever minted for another user. `UserHasRoleQuery` takes an explicit `UserId` (never the resolver) and is unaffected.

## Testing Decisions

A good test here asserts **external behavior at the trust boundary** — what status code or whose data a request yields — not the internal wiring of the resolver chain. Both unit and integration coverage are in scope (confirmed).

**Unit (`Tsz.Api.Tests`):**
- Effective `CurrentUserResolver`: returns the target user when `IImpersonationContext.TargetUserId` is set; returns the real user when not.
- `ImpersonationMiddleware`: real non-admin + header → 403; admin + missing target → 404; admin + malformed guid → 400; admin + valid target → context set, request proceeds.

**Integration (`Tsz.Api.Tests.Integration`):** Extend `TestAuthHandler` to let the real principal's `oid`/`email` be overridden per request (e.g. optional `X-Test-Oid` / `X-Test-Email` headers defaulting to the current fixed values) so a test can be "real caller is admin A" vs "real caller is non-admin B" while also sending `X-Impersonate-User`. New `ImpersonationEndpointsTests`. Seed admin **A** and plain user **B** (+ rows owned by B):
- As A impersonating B: a read-scoped list returns **B's** rows (DataScopeAccessor follows effective identity); a write (create/submit a timesheet) persists as **B**; an admin-only endpoint returns **403** (effective B is not Admin).
- As non-admin **B** with `X-Impersonate-User: A` → **403** (header ignored as a grant).
- As admin **A** with `X-Impersonate-User: <another Admin>` → **403** (no Admin targets).
- As admin **A** impersonating consultant **B**: Submit B's Draft week succeeds; the persisted week shows no Impersonator trace.
- `X-Impersonate-User` naming a non-existent user → **404**; malformed value → **400**.
- No header → behavior identical to today (regression guard).
- `RequireAdminOrSelf`: A impersonating B reaching B's `/users/{B}/…` succeeds ("self" = effective B); reaching A's resource is not self.
- `GET /api/users/impersonation-targets`: excludes Admin-role users; honors `search`; returns 403 for a non-admin caller.

**Prior art:** integration tests in `Tsz.Api.Tests.Integration` (use the `backend-integration-test` skill); `GetUsersPaged` for the keyset/search query shape; existing authz-handler unit tests under `Tsz.Api.Tests/.../Auth/*`.

## Out of Scope

- **Any persisted audit trail** of the Impersonator (actor field, audit table, per-action stamp) — explicitly excluded by ADR-0004; the future lever remains documented in `volatilities.md`.
- **Notifying the Impersonated User** that they are being impersonated.
- **A time-box** on impersonation duration (revisit only if audit requirements return).
- **Frontend role gating** as a security boundary — the API is the authority; FE gating is UX only (Phase 2 per `ui-plan.md`).
- **Impersonating Admins** — deliberately rejected, not a future toggle.
- **A new top-level route / NavLink** — entry points are the header button, the banner, and the Users-list row action.

## Further Notes

- Documentation deltas to land with the work: `requirements/login/login.md` (impersonation flips to scoped-in, read-write + no-audit); `requirements/login/ui-plan.md` (impersonation UI moves from out-of-scope to delivered); `volatilities.md` (note impersonation landed read-write without audit; audit table remains the documented future lever).
- **Stated consequence, by design:** an Admin impersonating a plain user who hits an admin-only endpoint gets a **403** — this is correct "view as user" behavior, not a defect.
- **Retrofit warning (ADR-0004):** adding traceability later requires a schema change *and* loses historical trace; the no-trace decision cannot be applied retroactively.
- Canonical terms (CONTEXT.md): **Impersonation**, **Impersonator** (the real Admin), **Impersonated User** (the target). Avoid sudo/switch-user/login-as, actor/operator, victim/subject/target.
