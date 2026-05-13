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

### 7. Web — full
- `packages/web/src/api/users.ts` — uses generated `components['schemas']['UserDto'|'CreateUserCommand'|'UpdateUserCommand'|'UserRole']` from `schema.ts`; wrappers `getCurrentUser`, `getUsers`, `getUserById`, `createUser`, `updateUser`, `removeUser`. `getCurrentUser` / `getUserById` map 404 → `null`.
- `packages/web/src/lib/current-user.ts` — `getCurrentUser` server fn wraps the API call.
- `packages/web/src/api/schema.ts` — regenerated against running dev API; also renames `Animal→AnimalDto`, `CreateAnimalRequest→CreateAnimalCommand`, `UpdateAnimalRequest→UpdateAnimalCommand`. `packages/web/src/api/animals.ts` + spec + `routes/_protected/animals/$id.tsx` updated for string (uuid) ids.

### 8. Web — `_protected` gate + `/no-access` (done)
- `src/routes/_protected.tsx` — after the session check, calls `getCurrentUser()`. `null` → `throw redirect({ to: '/no-access' })`. Exposes `{ user, currentUser }` on route context. Renders the post-login nav (Home / Animals / Users [admin-only] + signed-in-as + sign-out + theme toggle).
- `src/routes/no-access.tsx` — public route, "Your account isn't set up yet…" + sign-out button.
- `src/routes/__root.tsx` — stripped to the SSR shell + sign-in redirect; nav moved into `_protected.tsx` so admin link gates cleanly on `currentUser.role`.

### 9. Web — admin layout + Users CRUD (done)
- `src/routes/_protected/admin.tsx` — `beforeLoad` reads `currentUser` from route context, `throw redirect({ to: '/' })` if `role !== 'Admin'`.
- `src/routes/_protected/admin/users/index.tsx` — list (shadcn `Table`) with "New user" button, rows link to `$id`.
- `src/routes/_protected/admin/users/new.tsx` — create form (shadcn + TanStack Form + zod). Role is a styled native `<select>` (no shadcn Select component installed yet).
- `src/routes/_protected/admin/users/$id.tsx` — edit form (Name + Role; Email read-only) + Delete button (soft delete via API).

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
