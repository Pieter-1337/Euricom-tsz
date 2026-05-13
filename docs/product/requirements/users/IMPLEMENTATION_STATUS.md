# User pre-provisioning & access gate — implementation status

Worktree: `.claude/worktrees/users-module` (branch `worktree-users-module`).
Plan: `docs/product/requirements/users/plan.md`.

## Completed

### 1. Shared `AppDbContext`
- New: `packages/api/Tsz.Api/Common/Persistence/AppDbContext.cs` — `DbSet<Animal>`, `DbSet<User>`, `ApplyConfigurationsFromAssembly`.
- Removed: `Modules/Animals/AnimalDbContext.cs` and its migration (`Migrations/20260513115745_InitialCreate*.cs` + `AnimalDbContextModelSnapshot.cs`).
- Updated: `Program.cs` registers `AppDbContext` with `Data Source=tsz.db` (was `animals.db`); `AddInfrastructure<AppDbContext>()`; `AnimalSeeder` now takes `AppDbContext`.
- Updated: `tests/Tsz.Api.Tests.Integration/TestAuth/TestWebApplicationFactory.cs` and `tests/Tsz.Api.Tests/Infrastructure/EfCoreUnitOfWorkTests.cs` swapped to `AppDbContext`.

### 2. `Modules/Users/` scaffold
- `User.cs` — DDD-ish entity, private setters, soft-delete (`DeletedAt`), static `Create` seeds leave defaults (`Default*Days` consts).
- `UserRole.cs` — `enum { User=0, Admin=1, ClientManager=2 }`.
- `UserConfiguration.cs` — `HasQueryFilter(u => u.DeletedAt == null)`; unique indexes: `Email` filtered `WHERE DeletedAt IS NULL`, `EntraOid` filtered `WHERE EntraOid IS NOT NULL`; Role stored as string via `HasConversion<string>()`.
- `UserDto.cs` — `IEntityDto<User, UserDto>`.
- `UserSeeder.cs` — dev-only, seeds Pieter Bracke as `Admin` (idempotent by email).
- `UserEndpoints.cs` — `/api/users/me` (JWT only) + admin group `/api/users` with `RequireAuthorization(AuthorizationPolicies.RequireAdmin)`.

### 3. Auth abstractions (in `Tsz.Api/Common/Auth/`, **not** `Tsz.Infrastructure` — would create a circular dep with `User`)
- `ICurrentUser` — claim projection + `Task<User?> GetAsync(CancellationToken)`.
- `HttpContextCurrentUser` — scoped, per-request memoised `_cached`/`_loaded`. Match-by-`oid` → fallback match-by-email (case-insensitive) → link `EntraOid` on first login → `SaveChangesAsync` inside `GetAsync`. Claim keys: `oid`, `oid` long URI, `NameIdentifier`, `sub`; `email`, `preferred_username`, `ClaimTypes.Email`; `name`, `ClaimTypes.Name`.
- `AuthorizationPolicies.RequireAdmin` (constants holder).
- `RequireAdminRequirement` + `RequireAdminAuthorizationHandler` (scoped). Policy registered with `RequireAuthenticatedUser()` + `RequireAdminRequirement`.
- `JwtSecurityTokenHandler.DefaultMapInboundClaims = false` (set at module init in `Program.cs`) plus `options.MapInboundClaims = false` on the JwtBearerOptions in both Dev and non-Dev branches.

### 4. Users CQRS slices
- `GetCurrentUser.cs`, `GetUsers.cs`, `GetUserById.cs`, `CreateUser.cs` (`CreateUserResult { User, Conflict }` returns 409 on dup email), `UpdateUser.cs` (Name + Role; email immutable), `DeleteUser.cs` (soft delete via injected `TimeProvider`).
- FluentValidation validators on writes.
- Endpoints applied `ValidationFilter<T>` on POST/PUT.

### 5. Migration + dev seed
- `Migrations/20260513134407_Initial.cs` — both `Animals` and `Users` tables incl. filtered unique indexes.
- `UserSeeder` invoked in `Program.cs` only when `IsDevelopment()`. `AnimalSeeder` unchanged behaviour.
- JSON: `Program.cs` adds `JsonStringEnumConverter` to web JSON options — `role` serialises as `"Admin"|"User"|"ClientManager"`.

### 6. Tests
- Unit (`Tsz.Api.Tests`, **39 passing**):
  - `Builders/UserBuilder.cs` (NBuilder-style; with-extensions `WithName/WithRole/WithEntraOid/SoftDeleted`).
  - `Modules/Users/*HandlerTests.cs` for every slice; `*ValidatorTests.cs` for Create + Update.
- Integration (`Tsz.Api.Tests.Integration`, **23 passing**):
  - `UserEndpointsTests.cs` — `IAsyncLifetime` wipes Users table before each test (factory's in-mem DB is fixture-scoped).
  - Covers `/me` (404, OID-match, email→OID link), admin gate (403 for non-admin, 403 for unprovisioned), create (incl. 409 dup), update (incl. route-id mismatch), delete (soft-delete verified via `IgnoreQueryFilters`).
  - Static `JsonSerializerOptions` with `JsonStringEnumConverter` for `ReadFromJsonAsync<UserDto>` (otherwise the string `role` fails to deserialise).

### 7. Web — partial
- `packages/web/src/api/users.ts` — hand-typed `User`, `CreateUserRequest`, `UpdateUserRequest` + wrappers (`getCurrentUser`, `getUsers`, `getUserById`, `createUser`, `updateUser`, `removeUser`). `getCurrentUser` / `getUserById` map 404 → `null`.
- `packages/web/src/lib/current-user.ts` — `getCurrentUser` server fn wraps the API call.

> ⚠️ **TODO before merging**: run `bun --filter web gen:api` against the worktree's API to refresh `packages/web/src/api/schema.ts`, then replace the hand-typed shapes in `packages/web/src/api/users.ts` with `components['schemas']['UserDto'|'CreateUserCommand'|'UpdateUserCommand']` from the regenerated schema, and drop the `as any` / `as User` casts on the openapi-fetch client calls.
>
> The regen attempt in this session failed: the API needs `ASPNETCORE_ENVIRONMENT=Development` to load user-secrets that have the Azure AD `TenantId`/`ClientId`. Setting the env var inline with `VAR=value cmd` doesn't work in PowerShell. Easiest path next session: in pwsh `$env:ASPNETCORE_ENVIRONMENT='Development'; dotnet run --project packages/api/Tsz.Api --urls 'https://localhost:7215'` (or just use the existing `dev:api` script: `bun run dev:api`), then in another shell `bun --filter web gen:api`.

## Remaining (from `plan.md`)

### 8. Web: `_protected` gate + `/no-access`
- `src/routes/_protected.tsx` — after the existing `getSession()` check passes, call `getCurrentUser()`. `null` → `throw redirect({ to: '/no-access' })`. On success expose `{ session, currentUser }` on route context.
- `src/routes/no-access.tsx` — **public** route, no `_protected` parent. Static "Your account isn't set up yet. Ask an administrator." + sign-out button (use the `authClient.signOut` pattern from `__root.tsx`).
- `src/routes/__root.tsx` — when current route is `/no-access`, suppress or minimise the nav block. Easiest: render a minimal header conditional on `useLocation().pathname === '/no-access'`.

### 9. Web: admin layout + Users CRUD
- `src/routes/_protected/admin.tsx` — pathless layout, `beforeLoad` re-reads `currentUser` from route context, `throw redirect({ to: '/' })` if `role !== 'Admin'`.
- `src/routes/_protected/admin/users/index.tsx` — list with shadcn `Table`, link rows to `$id`.
- `src/routes/_protected/admin/users/new.tsx` — create form (shadcn + TanStack Form + zod, see `_protected/animals/$id.tsx` for the pattern).
- `src/routes/_protected/admin/users/$id.tsx` — edit form (Name + Role; Email read-only).
- Add admin nav link in `__root.tsx` gated on `currentUser.role === 'Admin'`, reading from route context (no re-fetch).

### Smoke test plan (from the plan, do after #8 + #9)
1. Log in as seeded admin (Pieter) → land on `/`, see `/admin/users` link, list shows self.
2. Add a second-tenant email via admin UI → sign out → sign in with that email → `/users/me` 200, `EntraOid` is set in DB.
3. Sign in with an unprovisioned tenant account → land on `/no-access`.
4. Sign in as a non-admin user → no `/admin/users` link; direct nav `/admin/users` redirects to `/`.

## Build + test commands

```pwsh
# Build everything
dotnet build packages/api/Tsz.Api/Tsz.Api.csproj

# Unit tests
dotnet test packages/api/tests/Tsz.Api.Tests/Tsz.Api.Tests.csproj --nologo

# Integration tests
dotnet test packages/api/tests/Tsz.Api.Tests.Integration/Tsz.Api.Tests.Integration.csproj --nologo

# Dev API (uses user-secrets for AzureAd)
bun run dev:api    # or: dotnet watch run --project packages/api/Tsz.Api --launch-profile https

# Regen TS schema (requires API running on https://localhost:7215)
bun --filter web gen:api
```

## Deviations from the plan worth flagging

- `ICurrentUser` lives in `Tsz.Api/Common/Auth/` rather than `Tsz.Infrastructure/Abstractions/`. The interface returns `User`, which is defined in `Tsz.Api`; pushing the interface down to `Tsz.Infrastructure` would invert the existing project reference (`Tsz.Api → Tsz.Infrastructure`).
- `JwtSecurityTokenHandler.DefaultMapInboundClaims = false` set at process entry (top of `Program.cs`) **and** `options.MapInboundClaims = false` on the JwtBearerOptions in both Dev and non-Dev branches, so the change isn't lost when the Dev-only debug logging block is removed later.
- `JsonStringEnumConverter` added to API JSON options so `role` is `"Admin"` not `1` over the wire. Affects the OpenAPI schema once regenerated — TS will see a string union, not a numeric enum.
- `Tsz.Api.csproj` already had `InternalsVisibleTo` for `Tsz.Api.Tests.Integration`, so `WebApplicationFactory<Program>` works without adding `public partial class Program;`.
