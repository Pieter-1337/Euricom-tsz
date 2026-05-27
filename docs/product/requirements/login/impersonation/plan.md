# Plan: User impersonation ("act as" another user)

## Goal

Let an **Admin** act as another user — both to *see the app exactly as that user sees it* (support / debug) and to *perform actions on their behalf* (e.g. submit or approve a timesheet for them). Impersonation is a request-scoped **effective identity** swap at the app layer; it never mints or swaps the real Entra access token.

## Use case & scope decisions

Canonical terms (see `CONTEXT.md`): **Impersonator** = the real Admin; **Impersonated User** = the target whose identity is assumed. The authorization & accountability model below is recorded in **ADR-0004**.

- **Replace-not-union identity.** While impersonating, the effective identity *replaces* the Impersonator's — the Impersonator can see and do **exactly** what the Impersonated User could, no more (Admin-only powers do not carry over) and no less. The only capabilities reserved to the real Admin are **stop/swap** impersonation, which are BFF-local (cookie) and never authorized by the C# API — so they need no special-casing in the authorization layer. There is **no attestation carve-out**: if the Impersonated User could Submit/Approve/Reopen a `TimesheetWeek`, so can the Impersonator while wearing their identity.
- **Read-write.** Writes are allowed while impersonating (act-on-behalf-of), not just reads. Deliberate, not a phased read-only-first rollout.
- **No trace (deliberate, v1).** Actions performed while impersonating — including the `TimesheetWeek` Status transitions — are attributed **solely** to the Impersonated User, with **no** persisted record of the Impersonator: no actor field, no audit table, no per-action stamp. Capability-transparency was chosen over attestation-traceability (ADR-0004). The only signal is a single transient `ILogger` line emitted when impersonation is engaged for a request — diagnostics, not a durable record.
- **Admin-only initiation.** Only a caller whose **real** DB identity holds the `Admin` role may impersonate.
- **No Admin targets.** An Admin may **not** impersonate a User who holds the `Admin` role — the one path with no legitimate use beyond acting untraceably at admin level. This is a valid-*target* restriction; it does not touch the replace-not-union capability model.
- **Transport = header.** The BFF forwards the target as an `X-Impersonate-User` request header alongside the Bearer token. The header is a **directive only** — see Trust Boundary.

## Context (current state from codebase research)

Identity and roles are **already decoupled from the token**, which is what makes this a contained change:

- **`packages/api`:**
  - `Tsz.Infrastructure/Auth/ICurrentUser.cs` / `HttpContextCurrentUser.cs` — reads only `oid` / `email` / `name` from JWT claims. **No roles in the token.**
  - `Modules/Users/.../Auth/CurrentUserResolver.cs` — resolves the DB `User` aggregate by `EntraOid` (email fallback + auto-link), and projects `ICurrentUserResolver.ResolveAsync` → `ResolvedUser(Id, RoleNames)`. **Roles come from the DB `User`, not the token.** Implements both `ICurrentUserResolver` (thin identity for authz) and `ICurrentUserAccount` (full `User`, e.g. for `/me`).
  - `Tsz.Infrastructure/Auth/ResolvedUser.cs` — `record (Guid Id, IReadOnlyCollection<string> RoleNames)` + `HasRole`.
  - Authz consumers that all funnel through `ICurrentUserResolver`: `RequireAdminAuthorizationHandler`, `RequireAdminOrSelfAuthorizationHandler`, `RequireAdminOrAnyClientManagerAuthorizationHandler`, and `DataScopeAccessor` (row-level ownership filtering). **This single seam is why the swap is contained.**
  - `AuthorizationPolicies.cs` — `RequireAdmin`, `RequireAdminOrAnyClientManager`, `RequireAdminOrSelf`; `AdminRoleName = "Admin"`.
  - `UserRole` enum: `User=0, Admin=1, ClientManager=2`. A user holds a *collection* of roles (`User.RoleAssignments`).
  - `CrossModule/UserHasRoleQueryHandler.cs` — takes an **explicit** `UserId`; does **not** go through the resolver, so it is unaffected by the identity swap (it answers about whatever user the caller names).
  - `Program.cs` — JwtBearer + `FallbackPolicy` requiring an authenticated user; `UseAuthentication()` then `UseAuthorization()`.
- **`packages/web` (BFF):**
  - `src/server/api-client.server.ts` — `bearerMiddleware` attaches `Authorization: Bearer …` via `auth.api.getAccessToken`. **This is where the impersonation header gets added.**
  - `src/server/auth-functions.ts` — `getSession` returns `SessionUser { id, name, email }`. **No roles** in the BFF session.
  - No frontend role gating exists yet (`requirements/login/ui-plan.md:131` defers it to Phase 2).
- **Tests:** `Tsz.Api.Tests.Integration/TestAuth/TestAuthHandler.cs` emits fixed claims (`oid/email = test-user-id / test@test.com`); roles still come from the seeded DB user matched by those claims.

## Trust boundary (non-negotiable)

The `X-Impersonate-User` header is a **directive, never a grant**. Authorization to impersonate is computed server-side on every request:

```
impersonation is honored  ⇔  valid JWT
                          ∧  the DB user of the JWT subject (oid) has role Admin
                          ∧  the target userId exists
                          ∧  the target user does NOT hold the Admin role
```

A non-Admin sending the header, or a header naming a missing user, or a header naming an Admin target, is **rejected** — the request must not silently fall back to the caller's own identity, because under read-write a silent fallback means an Admin could write as themselves while believing they're acting as the target. Fail **loud**, not fail-open and not fail-quiet. Status codes: real caller not Admin → **403**; target missing → **404**; target holds Admin → **403**; malformed guid → **400**.

## Architecture — real vs effective identity

We introduce a **dual-identity** model. Existing authz consumers keep using `ICurrentUserResolver` / `ICurrentUserAccount` unchanged; those now return the **effective** user. The **real** caller is resolved by a new, separate resolver used only for the impersonation gate.

```
JWT (real oid)                       X-Impersonate-User: <targetId>
   │                                          │
   ▼                                          ▼
IRealUserResolver  ──Admin?──►  ImpersonationMiddleware ──validates──► IImpersonationContext.TargetUserId
   (claims → DB User)                                                          │
                                                                               ▼
                              ICurrentUserResolver / ICurrentUserAccount (EFFECTIVE)
                              = target user if TargetUserId set, else real user
                                       │
            ┌──────────────────────────┼───────────────────────────┐
            ▼                          ▼                            ▼
   RequireAdmin*           RequireAdminOrSelf            DataScopeAccessor
   (effective roles)       ("self" = effective Id)       (effective ownership)
```

- Middleware sits **between `UseAuthentication()` and `UseAuthorization()`**: authentication has produced the real principal; impersonation must be validated and the context set *before* the authz handlers resolve the effective user.
- The effective `CurrentUserResolver` trusts `IImpersonationContext.TargetUserId` (already validated by the middleware) and loads that `User` by Id; otherwise it runs the existing claim-based real resolution.
- **Consequence to state plainly:** when an Admin impersonates a plain `User` and hits an admin-only endpoint, the *effective* user is not Admin → **403**. That is correct "view as user" behavior — you see exactly what they'd see, including being blocked.

## Files

### Backend — new

- `Tsz.Infrastructure/Auth/IImpersonationContext.cs` — request-scoped: `Guid? TargetUserId { get; }`, `bool IsImpersonating => TargetUserId is not null`, plus an internal setter (`SetTarget(Guid)`).
- `Tsz.Infrastructure/Auth/ImpersonationContext.cs` — scoped impl backing the above.
- `Tsz.Infrastructure/Auth/IRealUserResolver.cs` — `Task<ResolvedUser?> ResolveRealAsync(CancellationToken)` and `Task<User?>`-free thin view; resolves strictly from claims (no impersonation awareness). (Interface in Infrastructure so `Tsz.Api` middleware can depend on it; impl in Users module where the `User` aggregate lives.)
- `Tsz.Api/Auth/ImpersonationHeader.cs` — `public const string Name = "X-Impersonate-User";` (shared constant).
- `Tsz.Api/Auth/ImpersonationMiddleware.cs` — `IMiddleware`. When the header is present: resolve real user via `IRealUserResolver`; if null or not `Admin` → `403`; parse target `Guid` (malformed → `400`); load target `User` via `IUnitOfWork`/`IRepository<User>` (missing → `404`); if the target holds the `Admin` role → `403`; on success `impersonationContext.SetTarget(targetId)` and log one structured `ILogger` line (`real oid`, `targetId`). When header absent: no-op.

### Backend — modify

- `Modules/Users/.../Auth/CurrentUserResolver.cs` — split:
  - Extract the existing claim-based logic into **`RealUserResolver(ICurrentUser, IUnitOfWork) : IRealUserResolver`** (resolves real `User?` + `ResolvedUser?`, keeps the email→oid auto-link).
  - **`CurrentUserResolver(IRealUserResolver real, IImpersonationContext imp, IUnitOfWork uow) : ICurrentUserResolver, ICurrentUserAccount`** becomes the *effective* wrapper: `GetAsync` returns the target `User` (load by `imp.TargetUserId` via `IRepository<User>`) when impersonating, else the real `User`; caches per request. `ResolveAsync` projects that to `ResolvedUser`.
- `Modules/Users/.../UsersModule.cs` — register `RealUserResolver` (`IRealUserResolver`) and the effective `CurrentUserResolver` (`ICurrentUserResolver` + `ICurrentUserAccount`), both scoped.
- `Tsz.Api/Program.cs` — register `IImpersonationContext` → `ImpersonationContext` (scoped) and `ImpersonationMiddleware`; insert `app.UseMiddleware<ImpersonationMiddleware>()` **between** `app.UseAuthentication()` and `app.UseAuthorization()`.
- `tsz.slnx` — no new project (all in existing `Tsz.Infrastructure`, `Tsz.Api`, `Tsz.Modules.Users`), so no slnx change. (Confirm during impl.)

### Backend — tests

- `Tsz.Api.Tests.Integration/TestAuth/TestAuthHandler.cs` — allow the real principal's `oid`/`email` to be overridden per request (e.g. read optional `X-Test-Oid` / `X-Test-Email` headers, defaulting to the current fixed values) so tests can simulate "real caller is admin A" vs "real caller is non-admin B" while also sending `X-Impersonate-User`.
- `Tsz.Api.Tests.Integration/ImpersonationEndpointsTests.cs` — new (via the `backend-integration-test` skill). See **Tests**.
- `Tsz.Api.Tests/.../Auth/*` — unit tests for the effective `CurrentUserResolver` (swaps to target when context set; real otherwise) and for the middleware gate (non-admin → 403, missing target → 404).

### Backend — new (impersonation-target picker, decision B)

- `Modules/Users/.../Features/GetImpersonationTargets.cs` — new query + handler returning **non-Admin** users for the picker, with a `search` filter. Reuses the existing `KeysetQueryOptions` / `SearchableField` machinery (see `GetUsersPaged.cs`) but adds a base predicate excluding users whose `RoleAssignments` contain `Admin`. Keeps the picker from *offering* invalid targets; the middleware (Q3) is still the hard guard.
- `Modules/Users/.../Endpoints/UserEndpoints.cs` — map `GET /api/users/impersonation-targets?search=&...` under `RequireAdmin` (only a real admin, not impersonating, ever calls this).

### Frontend (`packages/web`)

- `src/server/impersonation.server.ts` — new server fns. The cookie is **session-bound and signed** — see **Cookie Security**:
  - `startImpersonation({ targetUserId, targetName })` — reads the current betterAuth session, sets `__Host-tsz_impersonate` to a signed payload `{ impersonatorId: <session user id>, targetId, targetName }`.
  - `stopImpersonation()` — clears the cookie.
  - `getImpersonation()` — verifies signature **and** `impersonatorId === current session user id`; returns `{ targetId, targetName } | null` (drives the banner; mismatch/invalid → treated as not impersonating).
- `src/server/api-client.server.ts` — in `bearerMiddleware`, call the same verify logic; emit `X-Impersonate-User: <targetId>` **only when** the signature is valid and `impersonatorId` matches the live session. Otherwise omit the header (and ideally clear the stale cookie).
- `src/lib/auth-client.ts` / sign-out path — clear `__Host-tsz_impersonate` on `signOut()` so impersonation never outlives the session.
- `src/routes/_protected.tsx` — **identity split** (the load-bearing UI change):
  - Header greeting → **real** identity. Switch line ~46 from `currentUser.firstName` to the session `user` already present in route context (line 31 returns `user`, currently unused for the greeting). "Hi, Pieter" stays the real admin even while impersonating.
  - Sidebar nav gating + all page data → **effective** identity (`currentUser`), unchanged — so impersonating a consultant correctly collapses the sidebar to what they see.
  - Header **inline "Impersonate" button** between the greeting and "Sign out", shown only when `currentUser` is Admin **and** not currently impersonating. Opens the target-picker dialog.
  - Red **banner beneath the header** when `getImpersonation()` is non-null: "You are impersonating **{targetName}**" + a **Stop** control calling `stopImpersonation()`. Full-width strip between `<header>` and the `flex min-h-0 flex-1` row. shadcn + destructive/red styling per `DESIGN.md`.
- `src/features/users/components/impersonate-dialog.tsx` — new: shadcn `Dialog` + `Command` searchable picker hitting `GET /api/users/impersonation-targets` (server fn); selecting a row calls `startImpersonation` then reloads to `/`.
- `src/features/users/...` (Users list) — **second entry point**: per-row "Impersonate" action on `/admin/users` (admin-only), same `startImpersonation` call. Kept in addition to the header button.
- Note: `/me` and all scoped endpoints return the **effective** (impersonated) user while the cookie is honored; the header greeting is the *only* place that intentionally shows the real identity.

## Steps

1. **Infrastructure abstractions** — add `IImpersonationContext` + `ImpersonationContext`; add `IRealUserResolver`; add `ImpersonationHeader.Name`.
2. **Split the resolver** — extract claim-based logic into `RealUserResolver : IRealUserResolver`; rewrite `CurrentUserResolver` as the effective wrapper over `IRealUserResolver` + `IImpersonationContext`. Keep the `ICurrentUserResolver` / `ICurrentUserAccount` public surface identical so no consumer changes.
3. **Middleware** — implement `ImpersonationMiddleware` with the trust-boundary checks (admin? target exists?) returning 403/400/404; set context + log on success.
4. **DI + pipeline** — register the new services in `UsersModule` / `Program.cs`; insert middleware between `UseAuthentication` and `UseAuthorization`.
5. **Impersonation-targets endpoint** — add `GetImpersonationTargets` query/handler (non-Admin users + `search`) and map `GET /api/users/impersonation-targets` under `RequireAdmin`.
6. **Backend unit tests** — effective resolver swap; middleware gate (incl. Admin-target → 403).
7. **Backend integration tests** — extend `TestAuthHandler` for per-request real identity; add `ImpersonationEndpointsTests`.
8. **BFF wiring** — `impersonation.server.ts` (start/stop/get + **signed, session-bound** `__Host-tsz_impersonate` cookie per Cookie Security); emit `X-Impersonate-User` in `api-client.server.ts` only when signature + `impersonatorId` match the live session; clear cookie on sign-out.
9. **Frontend UI** — `_protected.tsx` identity split (greeting → real `user`; nav/data → effective `currentUser`); admin-only inline header "Impersonate" button; red impersonation banner with Stop; `impersonate-dialog.tsx` searchable picker; per-row Impersonate action on `/admin/users`.
10. **Regenerate API client** — `bun --filter web gen:api` (picks up the new targets endpoint).
11. **No new NavLink** — the banner is global and the entry points are the header button + Users list; no new top-level route.
12. **Manual verify** — golden path below.

## Tests

**Unit (`Tsz.Api.Tests`)**
- Effective `CurrentUserResolver`: returns target user when `IImpersonationContext.TargetUserId` is set; returns real user when not.
- `ImpersonationMiddleware`: real non-admin + header → 403; admin + missing target → 404; admin + malformed guid → 400; admin + valid target → context set, request proceeds.

**Integration (`Tsz.Api.Tests.Integration`)**
- Seed admin **A** and plain user **B** (+ rows owned by B). As A with `X-Impersonate-User: B`:
  - A read-scoped list endpoint returns **B's** rows (DataScopeAccessor follows effective identity).
  - A write (e.g. create/submit a timesheet) persists as **B**.
  - An admin-only endpoint returns **403** (effective user B is not Admin) — confirms "view as user" gating.
- As non-admin **B** with `X-Impersonate-User: A` → **403** (header ignored as a grant).
- As admin **A** with `X-Impersonate-User: <another Admin>` → **403** (no Admin targets).
- As admin **A** impersonating consultant **B**, Submit B's Draft week → succeeds (attestation allowed, no carve-out); the persisted week shows no Impersonator trace.
- `X-Impersonate-User` naming a non-existent user → **404**; malformed value → **400**.
- No header → behavior identical to today (regression guard).
- `RequireAdminOrSelf`: A impersonating B accessing B's `/users/{B}/…` succeeds because **"self" = effective user (B)**; accessing A's resource is **not** self.
- `GET /api/users/impersonation-targets`: excludes Admin-role users; honors `search`; returns 403 for a non-admin caller.

## Cookie Security

The `tsz_impersonate` cookie is a **directive, not a grant** — it is only ever acted on when the *real* request is already an authenticated Admin (re-validated server-side every call, see Trust Boundary). It therefore does not need the auth cookie's bulletproofing to be safe; a forged/tampered cookie cannot escalate a non-admin (API 403) and a stale one does nothing once the session lapses (API 401).

Crucially it is **free of the OAuth-redirect constraints** that forced the session cookie to relax (`SameSite=Lax`, no `__Host-` prefix — see `login/plan.md` Cookie Security). The impersonation cookie is set/read only by **same-origin** server fns, so it can be hardened *beyond* the session cookie:

| Attribute | Session cookie | `__Host-tsz_impersonate` |
|---|---|---|
| `Secure` / `HttpOnly` | ✅ / ✅ | ✅ / ✅ (banner reads it server-side via `getImpersonation`) |
| `SameSite` | `Lax` (forced by OAuth chain) | **`Strict`** (no cross-site flow) |
| `__Host-` prefix | ❌ (BA path-scoped state cookies) | **yes** (`Path=/`, no Domain, Secure) |
| Signed | ✅ betterAuth | ✅ HMAC-SHA256 over payload, key = `BETTER_AUTH_SECRET` |
| Lifetime | session | session; cleared on stop + signOut |

**Session-binding (decided):** the signed payload is `{ impersonatorId, targetId, targetName }`. Both `getImpersonation` and `bearerMiddleware` honor the cookie **only when `impersonatorId === the current betterAuth session user id`**. This kills cookie reuse across sessions (admin A's stale cookie can't act if admin B later signs in on the same browser). The API per-request admin re-check remains the real guard regardless; binding + signing are defense-in-depth that let the BFF trust the value without the API being the sole line of defense.

## Lifecycle

- **Start:** admin invokes `startImpersonation` → signed, session-bound cookie set. Re-targeting just overwrites the cookie (no need to stop first).
- **Stop:** explicit `stopImpersonation` clears the cookie.
- **Sign-out:** `signOut()` clears `tsz_impersonate` — impersonation never outlives the betterAuth session.
- **Session expiry:** if the session lapses while the cookie lingers, the next API call has no valid Bearer → 401 → BFF redirects to login (and sign-out clears the cookie). Acceptable; no special handling.
- **No time-box** on impersonation duration in v1 (revisit only if audit requirements return).

## Edge cases

- **Admin impersonates an Admin** — **rejected (403)**. Valid-target restriction per ADR-0004.
- **Self-impersonation** (admin targets themselves) — moot: an Admin targeting themselves is targeting an Admin, so it's rejected by the no-Admin-target rule. (Net effect: harmless, and you'd never want to anyway.)
- **Stale target** — the impersonation cookie holds a target id; if that User is deleted or has roles changed mid-session, validation re-runs **every request**. A now-missing target → 404, a now-Admin target → 403; either way impersonation stops being honored and the Impersonator must stop/re-target. No stale capabilities persist.
- **No notification** — the Impersonated User is not notified that they are being impersonated. Out of scope for v1 (no-trace decision, ADR-0004).
- **Attestation transitions** — Submit/Approve/Reopen are **allowed** under impersonation when the Impersonated User's role permits them; no carve-out (ADR-0004). They leave no Impersonator trace by design.
- **`RequireAdminOrSelf` "self"** — resolves to the **effective** user, consistent with the rest of the model (stated above; covered by integration test).
- **`UserHasRoleQuery`** — takes an explicit `UserId`, never the resolver → unaffected.
- **`/me`** — returns the **effective** user while impersonating; the banner (driven by the cookie) is what tells the UI it's an impersonated view.
- **Caching** — `RealUserResolver` and the effective `CurrentUserResolver` each cache per request scope; the middleware resolves the real user before the effective resolver runs, so both are consistent within a request.
- **`disableDefaultScope` / token audience** — untouched. Impersonation is purely an app-level identity swap; the real Entra token (admin's) is still what's forwarded as Bearer. No token is ever minted for another user.

## Verify (golden path)

1. Sign in as an Admin. Header shows "Hi, {admin}" + an **Impersonate** button.
2. Click Impersonate → picker lists non-Admin users, search filters; Admins absent.
3. Pick a consultant → page reloads. **Header still says "Hi, {admin}"**; a **red banner** reads "You are impersonating {consultant}"; the sidebar collapses to the consultant's nav (no Users/Customers).
4. Open a timesheet → see/edit the consultant's data; **Submit** their Draft week succeeds; the persisted week carries no Impersonator trace.
5. Hit an admin-only route → blocked (effective user isn't Admin).
6. Inspect the cookie: `__Host-tsz_impersonate`, `Secure`, `HttpOnly`, `SameSite=Strict`; tampering the target id or using it under a different session → header not emitted, impersonation not honored.
7. Click **Stop** (or Sign out) → banner clears, header button returns, full admin nav restored.

## Documentation deltas (mirror `login/plan.md:15` style)

- `requirements/login/login.md:8` — "Optional: Support for user role impersonation" flips to **scoped-in**; update to reference this plan and the read-write + no-audit decisions.
- `requirements/login/ui-plan.md:131-133` — "Impersonation UI (listed as optional)" moves from *out of scope* to a delivered item (banner + admin action); update the exclusions list.
- `volatilities.md:16` — note impersonation has landed as read-write without audit; the audit table remains the documented future lever if accountability requirements change.

## Assumptions

- Dual-identity via a new `IRealUserResolver` + `IImpersonationContext` + effective `CurrentUserResolver`; existing authz consumers and their tests are **not** modified.
- Header transport (`X-Impersonate-User`), validated server-side every request; header is a directive, not a grant.
- BFF stores the active target in a signed, **session-bound** `httpOnly` `__Host-tsz_impersonate` cookie (`SameSite=Strict`); betterAuth session schema untouched.
- Read-write impersonation; **no** trace of the Impersonator — structured log line only (ADR-0004).
- Only `Admin` may impersonate; targets holding `Admin` are rejected. No attestation carve-out.
- Frontend impersonate action gated on `/me` roles for UX; API is the authority.
