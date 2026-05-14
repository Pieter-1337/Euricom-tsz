# Changelog

## 2026-05-14

docs: note browser restart requirement after mkcert -install

Add a line to certs/README.md telling contributors to fully quit
and reopen any running browsers after running mkcert -install —
a tab reload is not enough since browsers read the trust store
on startup. Saves a "why is my cert still untrusted" round trip.

## 2026-05-14

chore: move Azure AD values to appsettings.Development.json

Relocate dev TenantId/ClientId/Audience from base appsettings.json
to appsettings.Development.json so prod inherits no Azure config
by default and has to opt in explicitly. Base appsettings.json
keeps the AzureAd block with empty strings for discoverability.
Comment out UserSecretsId in Tsz.Api.csproj since we have no
actual secrets to store yet; trivial to uncomment when needed.

## 2026-05-14

chore: configure Azure AD settings

Fill in TenantId, ClientId, and Audience in appsettings.json for
local development. These are OAuth configuration identifiers
(not credentials). Overridable via appsettings.Development.json
or environment variables per the ASP.NET configuration hierarchy.

## 2026-05-14

refactor: unify dispatcher for commands and queries

Add IRequest<TR> base interface with ICommand and IQuery extending
it. Add IRequestHandler<TRequest, TR> base handler interface; both
ICommandHandler and IQueryHandler now extend it. Dispatcher.SendAsync
accepts IRequest<TR> and looks up IRequestHandler<,> reflectively,
so both commands and queries flow through the pipeline (validation
for commands, no-op for queries). Register all handlers against
IRequestHandler<,> in DI; endpoint signatures unchanged (accept
IDispatcher now instead of IQueryHandler directly). Enables logging,
authorization, and caching behaviors across all requests uniformly.
Also fixes missed GetUserByIdHandlerTests.cs constructor call from
name-split refactor.

## 2026-05-14

feat: unify form error handling, auto-clear server errors on edit

Server-side field errors now behave like client-side ones for the
purpose of disabling Save: any unresolved error (regardless of
source) blocks submit. Drop the onServer carve-out in
hasClientSideError and rename it hasFormError. Each bound field
component (Text/Number/Select/Textarea/Checkbox/Date) clears its
own errorMap.onServer on change, so a stale server error never
sticks to a field after the user starts fixing it. The form-level
banner clears itself the moment any field value changes, via a
form.store subscription in useFormServerErrors that snapshots
values when the error is set.

## 2026-05-14

feat: sidebar shell + emerald/slate theme tokens

Replace the top-nav bar with a two-column shell: a dark slate
header (brand + greeting + sign out + theme toggle) above a
sidebar (Home, and Users for admins) and a flex main pane.
Drop the max-w-3xl wrapper on the root so the layout owns its
own width. Repoint the shadcn theme tokens from neutral grey
to emerald-primary / slate-neutrals in both light and dark.

## 2026-05-14

feat: split User.Name into firstName/lastName

Replace the single Name field on User with FirstName + LastName
across the domain entity, DTO, EF configuration, and a new
SplitUserName migration. Both fields are required and max 128
chars. UserSeeder, CreateUser, UpdateUser, and all user test
fixtures update to the new shape. Web side regenerates the
OpenAPI schema; the admin user list/create/edit routes show
and edit the two fields separately.

## 2026-05-14

feat: per-year UserLeave + bulk leaves PUT; baseform polish

- Drop the LeaveTypes module: LeaveType + LeaveAllowed fold into
  Modules/Users as supporting reference data. Catalogue (Verlof, ADV
  dagen, Anciënniteit, Ziekte) seeded via EF HasData() with stable
  GUIDs so every environment gets it on Migrate().
- UserLeave gains Year; unique index on (UserId, LeaveTypeId, Year).
  CreateUser seeds one row per LeaveType for the current year via
  injected TimeProvider. UserSeeder tops up missing rows idempotently
  so existing admins get the new shape on next startup.
- Endpoint surface for leaves shrinks to two: GET
  /api/users/{userId}/leaves?year= and atomic bulk PUT with the full
  year set. No POST/DELETE/PUT-by-id. UpdateUserLeavesValidator
  pre-loads referenced rows + joined LeaveType in one query and
  validates per-item via RuleForEach.ChildRules so failures key under
  items[i].*. Single LeavesModel migration replaces the prior
  LeavesModel+SimplifyLeaves pair.
- Admin user edit page: replace per-row dialog with a single
  useAppForm-driven Leave overview table for the current year.
  Limited rows render NumberField; Unlimited rows render a muted
  "Unlimited" label. Taken/Balance render "—" until timesheets ship.
- Baseform fixes: dirty state now reads TanStack Form's
  state.isDefaultValue so form.reset(updated) re-baselines correctly
  after save; Cancel only renders when the form is dirty (custom-
  handler cancels still always render); FormActions defaults to a
  left-aligned button row so every form is consistent.

## 2026-05-14

chore(web): prettier sweep across web package

No logical changes — re-runs of formatter on previously unformatted
files.

## 2026-05-14

feat(web): shared form base + migrate admin user forms

- useAppForm via createFormHook with bound field components (TextField,
  NumberField, SelectField, TextareaField, CheckboxField, DateField)
  and bound form components (FormActions, SubmitButton, CancelButton,
  FormErrorBanner)
- useFormServerErrors hook: ProblemDetails -> errorMap.onServer
- FormActions: Save disabled when unchanged from baseline (deep-equal);
  Cancel opt-in via `cancel` prop (true = reset, fn = custom action)
- Button: cursor-pointer / cursor-not-allowed for proper hover
  affordance
- Migrate admin users new/edit + EditLeaveDialog to the new base
- New user submit redirects to /admin/users/$id; Delete moved to header
- Update frontend-form skill for the new conventions

## 2026-05-14

refactor(api): standardise UpdateUserLeaveValidator on .WithError()

Last legacy `.WithErrorCode(CommonErrors.Invalid.Code)` chain replaced
with the typed `.WithError(CommonErrors.Invalid)` helper, so
ValidationFailure.CustomState carries the SmartEnum and the global
exception handler can read the category directly instead of falling
back to Validation. Custom message preserved via trailing .WithMessage.

Test strengthened to assert CustomState + Category.

## 2026-05-14

docs+test: post-refactor follow-ups for the result pattern

- Reflect the new dispatcher / typed-errors / ProblemDetails contract
  in agent conventions, architecture, and the backend-slice /
  unit-test / integration-test skill files.
- Close 4 coverage gaps identified by audit: AddUserLeave happy-path
  integration, UpdateUserLeave 400 ProblemDetails body shape,
  end-to-end 500 smoke via a throwing test-only endpoint, and the
  unlimited-type-with-days validator failure case. 81 unit + 34
  integration green.

## 2026-05-14

feat: dispatcher + typed errors via ProblemDetails; leaves data model

Two coupled changes landing together:

* Result pattern. In-house IDispatcher + ValidationBehavior pipeline
  replaces direct handler.HandleAsync calls. Per-module ErrorCode
  SmartEnums (UserErrors, LeaveTypeErrors, UserLeaveErrors) with
  categories (NotFound/Conflict/Forbidden/Validation) attached to
  FluentValidation failures via .WithError(). GlobalExceptionHandler
  catches ValidationException and emits RFC 7807 ProblemDetails
  (404/409/403/400 by highest-severity category; 500 generic for
  unexpected, no detail leaked). ValidationFilter removed. Frontend
  ApiRequestError parses application/problem+json; forms surface
  userMessage + per-field errors via TanStack Form errorMap.onServer.
  IUnitOfWork Begin/Close removed (unused; EF implicit txn suffices).

* Leaves model. New LeaveType (seeded reference data) + per-user
  UserLeave entity. CreateUser seeds a UserLeave row per LeaveType.
  Admin endpoints for LeaveType CRUD and per-user leave management.
  Drops User's flat HolidayDays/AdvDays/AncienniteitDays/SicknessDays
  columns.

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
