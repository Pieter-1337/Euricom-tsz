# Changelog

## 2026-05-14

refactor: drop AppDbContext DbSets; route plumbing via IRepository

Extend IRepository with a per-call ignoreQueryFilters flag and a
BatchHardDeleteAsync primitive so seeders, the auth user resolver, and
integration tests can run through IUnitOfWork instead of reaching for
AppDbContext directly. With the abstraction now able to express soft-
delete bypass, the explicit DbSet<User> property comes off AppDbContext
— entities are still registered via ApplyConfigurationsFromAssembly,
and IRepository<T> resolves them through context.Set<T>().

EfCoreRepository.BatchHardDeleteAsync uses ExecuteDeleteAsync on
relational providers and falls back to load+RemoveRange+SaveChanges on
InMemory, so integration tests get the same observable behaviour
without depending on the relational SQL path.

Update the backend-module, backend-integration-test, and ef-seed
skills to reflect the new conventions: no DbSet step, tests cleanup
via BatchHardDeleteAsync, seeders take IUnitOfWork.

## 2026-05-13

feat(web): finish entra access gate UI — /no-access, admin users CRUD

Wire the access gate end-to-end on the BFF: _protected.beforeLoad now
calls getCurrentUser() after the session check and redirects null
results to a new public /no-access route. Pull the post-login nav out
of __root.tsx and into _protected.tsx so the admin link can gate
cleanly on the resolved currentUser.role (no extra fetch).

Add the admin layout + Users CRUD pages (list / new / edit-with-delete)
backed by createServerFn + TanStack Form + zod + shadcn primitives.
Role uses a styled native <select> for now.

Regen packages/web/src/api/schema.ts against the running dev API.
Replace the hand-typed shapes in users.ts with the generated
components['schemas']['UserDto'|'CreateUserCommand'|'UpdateUserCommand'|
'UserRole']. The regen also surfaced the Animals rename
(Animal→AnimalDto, *Request→*Command) plus the move to uuid ids —
animals.ts, its spec, and the $id route are realigned.

## 2026-05-13

feat: add users module and entra access gate (api-side, partial web)

Replace the per-module AnimalDbContext with a shared AppDbContext that
ApplyConfigurationsFromAssembly-discovers entity configs. Add a Users
module mirroring the Animals shape: DDD-ish entity with private
setters and static Create, soft-delete via global query filter,
filtered unique indexes on Email (per non-deleted) and EntraOid, role
stored as string, leave-default columns seeded at creation.

Introduce ICurrentUser + HttpContextCurrentUser in Tsz.Api/Common/Auth
(not Tsz.Infrastructure — the interface returns User, so pushing it
down would invert the project reference). The implementation is
scoped, memoises a single per-request DB roundtrip, matches on the
Entra oid claim with email-fallback and links EntraOid on first
login. A RequireAdmin authorization policy delegates to this same
abstraction so policy + handlers see one source of truth for role.
DefaultMapInboundClaims is disabled so oid/sub/email arrive
unmolested.

Endpoints: GET /api/users/me (JWT only, 404 when unprovisioned) plus
admin-gated CRUD on /api/users with soft delete. JsonStringEnumConverter
makes role serialise as a string union over the wire. Initial EF
migration creates both tables; a dev-only UserSeeder writes one Admin
row idempotently.

Tests: 39 unit (handlers, validators, UnitOfWork now on AppDbContext)
and 23 integration (in-memory DB per fixture, IAsyncLifetime wipes
users between tests, covers /me 404 + oid match + email→oid link,
RequireAdmin 403 paths, dup-email 409, soft-delete hidden by query
filter).

Web side adds a hand-typed users.ts API wrapper and a current-user
server fn — schema regen still pending (see
docs/product/requirements/users/IMPLEMENTATION_STATUS.md for the
handoff incl. route/UI work still to do).

## 2026-05-12

docs: inline AGENTS.md content into CLAUDE.md and stress subagents

Previously CLAUDE.md was a single-line pointer to AGENTS.md. Inline
the full agent guidance and add an `(important!)` emphasis on
spawning subagents to keep context lean.

docs: align login plan and ui-plan with current auth implementation

Update plan.md and ui-plan.md to match the implementation that
actually shipped: SQLite-backed sessions instead of stateless cookies,
`BETTER_AUTH_SECRET`/`BETTER_AUTH_URL` env var names, the rolled-back
`__Host-` prefix and `SameSite=Strict` (now `Lax` defaults), the
`scope` (singular) vs `scopes` field-name pitfall and
`disableDefaultScope: true` requirement, the dev-only JwtBearer debug
hooks on the API, and the auto-redirect from RootLayout instead of a
manual Login button. Delete the obsolete login-database.plan.md — the
migration it described is complete.

fix: correct scope field name so entra returns api access token

Better Auth's Microsoft social provider reads `scope` (singular), not
`scopes`. The misnamed field meant the configured
`api://.../access` scope was silently dropped during sign-in, so
Microsoft returned a Graph access token instead of one for the API
and signature validation failed. Rename the field, disable the
hardcoded `User.Read` default to keep the request scoped to a single
resource, and source the API client id from `API_CLIENT_ID`.

Add dev-only JwtBearer debug events on the API to surface inner
exceptions and decoded claims when validation fails. Pre-fill the
public Entra Instance URL in appsettings so only secrets stay in
user-secrets.

fix: persist sessions to bun:sqlite and relax cookie hardening for OAuth

Switch Better Auth's database from memoryAdapter to a bun:sqlite-backed
file (auth.db) so sessions survive SSR HMR restarts instead of being
wiped on every reload. Add a Microsoft profile mapper so the BetterAuth
user id is the Entra `oid` (falling back to `sub`). Comment out the
`__Host-timesheetzone` cookie prefix and drop SameSite=Strict on
session_token: the post-OAuth redirect chain (microsoft.com → /callback
→ /) is cross-site per the SameSite spec, so a Strict cookie is dropped
on the first / request and the route guard sees a null session. Lax
(BA default) survives the redirect; __Host- is also incompatible with
BA's path-scoped OAuth state cookies. Ship a bun:sqlite type shim so
the Bun built-in module typechecks.

feat: serve web and api over local HTTPS for OAuth dev

Wire up mkcert-generated certs in Vite's dev server and switch
BETTER_AUTH_URL and API_URL to https. Enable HttpsRedirection in the
.NET API and run dotnet watch with the https launch profile. Add a
check:certs preflight in the web dev script and ignore the cert files
(keeping certs/README.md tracked). Includes WIP debug logs in
getSession and __root to investigate a session-null-after-OAuth bug.

fix: scope SameSite=Strict to session cookie only to resolve state_mismatch

State and PKCE cookies need SameSite=Lax (Better Auth's default) to
survive the cross-site redirect from Microsoft during OAuth. Applying
Strict globally stripped those cookies on the redirect, causing a
state_mismatch error. Add inline comments explaining the rationale and
update plan and ui-plan docs accordingly.

feat: harden session cookies with __Host- prefix and Strict SameSite

Apply __Host-timesheetzone cookie prefix and SameSite=Strict, Secure,
HttpOnly, Path=/ to all betterAuth session cookies. The __Host- prefix
prevents subdomain cookie injection; Strict SameSite blocks cross-site
request inclusion. Documents the requirements in the login plan.

## 2026-05-11

feat: add Microsoft Entra authentication via Better Auth

Set up Better Auth with the Microsoft social provider for Entra
ID SSO. Adds protected route layout, auth API handler, session
management with tanstackStartCookies plugin, and initialises the
in-memory adapter with the required model tables. Removes old
unprotected routes and replaces the app entry point with a
redirect to the protected section.

## 2026-05-11

docs: split product requirements into subfolders and add login plan

chore: reformat changelog to date-based format and update commit skill

style: remove commented-out alias block from vite config
chore: replace vite alias with tsconfig baseUrl for path resolution
docs: add product requirements, architecture, and agent convention docs
chore: consolidate vitest config into vite config and add @tests alias
chore: update test to use new fetchutils
feat: extract api client module, add animals api tests, and fix age type errors
chore: add Ref and Exa MCP servers and commit skill changelog step
test: add unit tests for ValidationFilter
chore: add AGENTS.md with monorepo overview and conventions pointers
feat: fix OpenAPI schema to emit strict number types and required fields
feat: add validate skill, search filter feature, and test infrastructure

## 2026-05-08

chore: add claude skills directory with implement, plan, validate, and skill-creator skills
docs: rewrite commit skill with split, secret, push rules
chore: move claude.md to repo root as CLAUDE.md
chore: add dbhub MCP server config for animals.db
chore: add commit slash command and skill
Enhance REVIEW.md with strict OpenAPI specifications and required field handling for improved TypeScript type generation
Update REVIEW.md to include strict OpenAPI specs for improved TypeScript type generation
Add onSubmit handler to save animal data in REVIEW.md
Add REVIEW.md for guidelines and improvements

## 2026-05-07

Retrofit animals routes with shadcn UI primitives
Validate animal server fn inputs with zod
Add edit form on animal detail and route fetches via Start server
Revert animals loaders to direct fetch without createServerFn
Ignore Visual Studio .vs folder
Restructure animals routes and pin fetches to server
Reformat OpenAPI schema with single quotes
updated settings.json schema
Add startup database seeding for animals
Initial commit
