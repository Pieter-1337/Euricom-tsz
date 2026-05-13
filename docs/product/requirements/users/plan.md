# Plan: User pre-provisioning & access gate (POC)

## Goal

Add an admin-managed user list to the C# API and gate access to the app on whether a logged-in Entra identity matches a row in that list. Out-of-list Entra users see a "no access — ask an admin" page instead of the app shell.

## Context

- Entra owns *authentication* (who you are). It is the company-wide AD; the app does not provision identities there.
- The API's `Users` table owns *authorization* (who is allowed in, what role, what leave defaults). Admins manage this list.
- POC scope: a timesheet app for internal employees. Eventually all tenant members should be allowed, but for the POC the explicit allowlist gives us role assignment + leave defaults + a clean place to hang the rest of the user-scoped data model.
- Auth is already wired per `docs/product/requirements/login/plan.md`: betterAuth on the BFF, Entra access token forwarded as Bearer to the API. The session in `packages/web/auth.db` exists for *anyone* in the tenant who completes the OAuth flow — that is unchanged. The new gate sits one layer in: the BFF asks the API "who am I in your world?" and if the API says "nobody", we render the no-access screen.

## Architecture

```
Browser ──► BFF (BA session exists for any Entra user)
              │
              ├── beforeLoad in _protected → server fn getCurrentUser()
              │       │
              │       └── API GET /users/me  ──► 200 + User     → enter app
              │                                  404            → redirect /no-access
              │
              └── /no-access route (renders even with BA session)
```

- BA session and `User` are decoupled. A BA session can exist without a `User` row; that is the "logged in but not authorized" state.
- The gate lives on the **API**, not the BFF. `GET /users/me` is the only endpoint that authenticated-but-unauthorized users can hit successfully (it returns `404` to signal "you're authenticated, but not provisioned"). Every other endpoint requires a matched `User`.

## Data model (`packages/api`, new `Users` module)

Mirror the `Animals` module shape: per-module `DbContext`, EF configuration, service, endpoints, contracts.

### `User` entity

| Field              | Type                                    | Notes                                                                                            |
| ------------------ | --------------------------------------- | ------------------------------------------------------------------------------------------------ |
| `Id`               | `Guid`                                  | Internal PK.                                                                                     |
| `EntraOid`         | `string?`                               | Entra Object ID. Null until first successful login links it. Unique when set.                    |
| `Email`            | `string`                                | Required, unique (case-insensitive) **among non-deleted rows**. The lookup key admin sees and types. |
| `Name`             | `string`                                | Display name. Admin-typed at creation; overwritten on first login from Entra `name` claim.       |
| `Role`             | `enum { Admin, User, ClientManager }`   | Stored as string for forward-compat.                                                             |
| `HolidayDays`      | `decimal`                               | Default leave allowance. Seeded at create: `20`.                                                 |
| `AdvDays`          | `decimal`                               | Default leave allowance. Seeded at create: `5`.                                                  |
| `AncienniteitDays` | `decimal`                               | Default leave allowance. Seeded at create: `0`.                                                  |
| `SicknessDays`     | `decimal`                               | Default leave allowance. Seeded at create: `0`.                                                  |
| `DeletedAt`        | `DateTimeOffset?`                       | Soft delete marker. All queries filter `DeletedAt IS NULL` via an EF global query filter.        |

> The four leave-default columns are flat on `User` because the set of leave types is closed (4 fixed enum values), every user always has all four, and they're static settings — not a per-year ledger. The full per-year leave balance UI from `users.md` (Total/Taken/Balance per year) is **out of scope** for this plan; when it lands it will be its own `LeaveBalance` table keyed by `UserId + Year + Type`, separate from these defaults.

### DbContext

Single shared `AppDbContext` backed by `tsz.db`. SQLite handles a multi-table DB fine; per-module DbContexts only pay off when you need physical isolation (which we don't) and they make cross-module FKs (`Timesheet.UserId → User.Id`, coming later) painful.

Layout:

```
Modules/
  Users/
    User.cs
    UserConfiguration.cs          ← IEntityTypeConfiguration<User>
    ...
Common/
  Persistence/
    AppDbContext.cs               ← DbSet<User>; ApplyConfigurationsFromAssembly
```

Each module owns its `IEntityTypeConfiguration<>`; `AppDbContext.OnModelCreating` calls `ApplyConfigurationsFromAssembly` so adding a new module is one `DbSet<>` + one config file with no central-context edits.

### Migration

`dotnet ef migrations add Initial --context AppDbContext` generates the initial migration. Keep the `EnsureCreated` pattern from `Program.cs` for dev provisioning, pointed at `AppDbContext`.

Seed in `Development` only: one hardcoded admin row (Pieter's email, role=Admin, default leaves). Without it the dev DB is a chicken-and-egg lockout.

## API surface

All endpoints require an authenticated Entra JWT (existing fallback policy). Endpoints additionally check the `User` row exists where noted.

| Method | Path                | Auth                     | Body / Response                                                                                          |
| ------ | ------------------- | ------------------------ | -------------------------------------------------------------------------------------------------------- |
| GET    | `/users/me`         | JWT only                 | `200 UserDto` if `oid`/email matches a row, else `404`. Side-effect: on match-by-email, write `EntraOid` if null. |
| GET    | `/users`            | JWT + `Role == Admin`    | `200 UserDto[]`                                                                                          |
| GET    | `/users/{id}`       | JWT + `Role == Admin`    | `200 UserDto` / `404`                                                                                    |
| POST   | `/users`            | JWT + `Role == Admin`    | `CreateUserRequest { name, email, role }` → `201 UserDto`. Seeds leave default columns per the prefill rule. |
| PUT    | `/users/{id}`       | JWT + `Role == Admin`    | `UpdateUserRequest { name, role }` (email immutable — see Edge Cases) → `200 UserDto`                    |
| DELETE | `/users/{id}`       | JWT + `Role == Admin`    | `204`. Soft delete: sets `DeletedAt = UtcNow`. Row stays in DB, vanishes from all queries via the EF global filter. |

### `ICurrentUser` — single inject point for caller identity

A single abstraction that projects claims **and** resolves the domain `User` row. This is the one service handlers, validators, and the `RequireAdmin` policy inject. Registered **scoped**, so the resolved row is cached for the lifetime of one HTTP request — no `HttpContext.Items` stringly cache, no re-querying inside the same request.

```csharp
public interface ICurrentUser
{
    // Claim projection — synchronous, no DB.
    string? EntraOid { get; }              // "oid" claim (preferred), fall back to NameIdentifier
    string? Email { get; }                 // "email" / "preferred_username"
    string? Name { get; }                  // "name"
    bool IsAuthenticated { get; }

    // Domain entity — async, DB-backed, cached per request scope.
    // Carries the link-on-first-login side effect (see Identity mapping below).
    // The authoritative source for Role — Entra app-roles are deliberately ignored.
    Task<User?> GetAsync(CancellationToken ct = default);
}
```

> No `EntraRoles` / app-role claim projection. Roles live on the `User` row, owned by admins. Authorization (including `RequireAdmin`) always goes through `GetAsync().Role`, never through `HttpContext.User.IsInRole(...)` or a claim list. Keeps a single source of truth: even if someone later assigns Entra app-roles, they won't accidentally grant access here.

Implementation `HttpContextCurrentUser` (in `Tsz.Api/Common/Auth/`) holds an `_cached` field plus a `_loaded` flag so a null result is also memoised — one DB roundtrip per request maximum, zero for endpoints that never call `GetAsync`.

Wiring (in `Program.cs` or `ServiceCollectionExtensions.AddInfrastructure`):

```csharp
services.AddHttpContextAccessor();
services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
```

**Entra claim choice.** Entra `sub` is per-app (rotates if the app registration is recreated); `oid` is the tenant-stable Object ID. We key off `oid` first, fall back to `NameIdentifier`/`sub` as a safety net. Also set `JwtSecurityTokenHandler.DefaultMapInboundClaims = false` (or check both short and long-URI claim names) so `oid` isn't silently remapped to `http://schemas.microsoft.com/identity/claims/objectidentifier`.

### Identity mapping (inside `ICurrentUser.GetAsync`)

```text
EntraOid claim present?
  └── row WHERE EntraOid = oid → cache + return.
  └── miss → fall through to email match.
Email/preferred_username claim present?
  └── row WHERE Email = claim (case-insensitive) AND EntraOid IS NULL
        → set EntraOid = oid, SaveChanges, cache + return.
  └── otherwise (no row, or row's EntraOid already locked to a different oid) → cache null, return null.
```

The "match by email then lock to oid" pattern is what makes admin UX bearable: admins know emails, not object IDs. After one successful login the `EntraOid` is permanent and email becomes irrelevant for lookup (it can drift in Entra without breaking us). Collisions (recreated-in-Entra user, OID changed) collapse into the null/404 path — admin's problem to recreate the row.

> The link-on-first-login `SaveChangesAsync` runs inside `GetAsync`. It's idempotent (only fires when `EntraOid IS NULL`) and commits before the handler runs — if the handler later fails, the link stays. Acceptable: linking is monotonic and the alternative (deferring the link to a separate write) adds ceremony for no real safety gain.

### Admin authorization

Policy `RequireAdmin` is a tiny `IAuthorizationHandler` that injects `ICurrentUser`, awaits `GetAsync()`, and `Succeed`s only when the row exists and `Role == Admin`. Apply with `.RequireAuthorization("RequireAdmin")` on the admin routes. No DB call duplication — the handler shares the per-request `_cached` value with anything else in the request that already called `GetAsync()`.

### Endpoint usage

- **`/users/me`**: `var u = await currentUser.GetAsync(); return u is null ? Results.NotFound() : Results.Ok(map(u));`
- **CRUD endpoints**: protected by `RequireAdmin`; admin operations don't need to inspect the caller's own row beyond the policy check.
- **Future timesheet/leave endpoints** (out of scope here): `var owner = await currentUser.GetAsync() ?? throw …; timesheet.AssignTo(owner.Id);` — one inject, one call, no claim parsing in handlers.

## Frontend (`packages/web`)

### New files

- `src/lib/current-user.ts` — server fn `getCurrentUser()` that calls `GET /users/me` via `api.server.ts`. Returns `User | null` (null on 404, throws on anything else).
- `src/routes/no-access.tsx` — public route, no `_protected` parent. Renders "Your account isn't set up yet. Ask an administrator to add you." Includes a `Sign out` button.
- `src/routes/_protected/admin.tsx` — pathless or nested layout; `beforeLoad` re-reads the cached current user from route context and `throw redirect('/')` if `role !== 'Admin'`.
- `src/routes/_protected/admin/users/index.tsx` — list (shadcn `Table`).
- `src/routes/_protected/admin/users/new.tsx` — create form (shadcn + TanStack Form).
- `src/routes/_protected/admin/users/$id.tsx` — edit form.

### Modify

- `src/routes/_protected.tsx` — after the existing `getSession` check passes, call `getCurrentUser()`. If `null`, `throw redirect({ to: '/no-access' })`. If set, expose `{ session, currentUser }` via route context so child routes can read `currentUser.role` without re-fetching.
- `src/routes/__root.tsx` — when on `/no-access`, suppress the "Signed in as …" nav block or replace it with a minimal header.
- After API surface lands: `bun --filter web gen:api` to refresh schema types.

### Nav

Admin-only link to `/admin/users` shown in `__root.tsx` when `currentUser.role === 'Admin'`. Read from route context, not a re-fetch.

## Steps

1. **API: shared `AppDbContext`** — create `Common/Persistence/AppDbContext.cs` with `ApplyConfigurationsFromAssembly`, register it in `Program.cs` against `Data Source=tsz.db`.
2. **API: new Users module** — scaffold `Modules/Users/`: `User.cs`, `UserConfiguration.cs` (configures the soft-delete global filter `HasQueryFilter(u => u.DeletedAt == null)` and the case-insensitive email unique index scoped to `DeletedAt IS NULL`), `UserService.cs`, `UserContracts.cs`, `UserEndpoints.cs`. Add `DbSet<User>` to `AppDbContext`.
3. **API: initial migration + dev seed** — `dotnet ef migrations add Initial --context AppDbContext`. In `Program.cs`, after `EnsureCreated`, seed one admin row (Pieter's email, role=Admin, default leaves) in `Development` only.
4. **API: `ICurrentUser` scaffolding**:
   - `Tsz.Infrastructure/Abstractions/ICurrentUser.cs` — interface with claim props + `Task<User?> GetAsync()`.
   - `Tsz.Api/Common/Auth/HttpContextCurrentUser.cs` — impl with scoped `_cached`/`_loaded` memoisation and the link-on-first-login DB write.
   - Disable `JwtSecurityTokenHandler.DefaultMapInboundClaims` (or check both `oid` and the long-URI form).
   - Register `IHttpContextAccessor` + `AddScoped<ICurrentUser, HttpContextCurrentUser>()`.
   - Unit test the claim projection with a fake `IHttpContextAccessor`; integration-test `GetAsync` via `TestAuthHandler` claims + `UseInMemoryDatabase`.
5. **API: `RequireAdmin` policy** — `IAuthorizationHandler` that injects `ICurrentUser`, awaits `GetAsync()`, succeeds when row exists and `Role == Admin`. No own caching — relies on `ICurrentUser`'s scoped memoisation.
6. **API: endpoints** — implement the six routes above. `/users/me` is one call to `currentUser.GetAsync()` (the link-on-first-login is handled inside `ICurrentUser`, not in the endpoint). Rest are straightforward CRUD using a `UserService`. `DELETE` sets `DeletedAt`, doesn't remove.
7. **Regen schema** — `bun --filter web gen:api`.
8. **Web: `current-user.ts` server fn** — call `/users/me`, map 404→null, anything else→throw.
9. **Web: `_protected.tsx` gate** — after session check, call `getCurrentUser()`; null → redirect `/no-access`; else expose on route context.
10. **Web: `/no-access` route** — public, minimal page + sign-out.
11. **Web: admin layout + users CRUD pages** — list, new, edit forms. Use shadcn components (per `feedback_shadcn`).
12. **Web: nav** — admin link in `__root.tsx` gated on `currentUser.role`.
13. **Smoke test (manual)**:
    - Log in as the seeded admin → land on `/`, see `/admin/users` link, list shows self.
    - Create user with my second tenant email (or a colleague's) → sign out → sign in with that email → `/users/me` returns 200, `EntraOid` is now set in DB.
    - Sign in with an unprovisioned tenant account → land on `/no-access`.
    - Sign in as a non-admin user → no `/admin/users` link; direct navigation redirects to `/`.

## Edge cases & non-goals

- **Email vs OID drift.** Admins type emails; OIDs are stable. We bridge with the link-on-first-login. After link, email is display-only — even if Entra changes it, we still match by OID.
- **Recreated-in-Entra user (OID changed, email reused).** `/users/me` returns 404 (same path as "not provisioned"). The old row stays locked to the dead OID; admin recreates with a fresh row. Email uniqueness applies only to non-deleted rows, so admin can soft-delete the stale row first if they need the same email back.
- **Role changes.** A user whose role flips Admin→User mid-session still sees admin UI until they hard-refresh (route context is loader-scoped). Acceptable for POC. If we care, invalidate the `_protected` loader on role-change or move role into the BA session.
- **Self-service.** Out of scope. No "request access" form; the no-access page is a dead end with a `mailto:` or Slack link to the admin. Pick the contact mechanism when wiring the page.
- **Leave management UI.** The schema lands here; the per-user leave settings + leave-overview UIs from `users.md` / `leave-overview.md` are separate plans.
- **Bootstrap chicken-and-egg.** Fresh DB has no admin → nobody can create the first admin. Solved by the dev-only seed in step 3; for staging/prod we'll need a one-shot CLI or env-var-driven seed. Out of scope here.
