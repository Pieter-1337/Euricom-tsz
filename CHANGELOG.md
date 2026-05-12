# Changelog

## 2026-05-12

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
