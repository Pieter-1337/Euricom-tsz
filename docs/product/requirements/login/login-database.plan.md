# Plan: Persistent SQLite for Better Auth

## Goal

Replace the in-memory adapter with persistent SQLite (`better-sqlite3`) so sessions, accounts, and refresh tokens survive process restarts and Vite SSR module isolation — unblocking the OAuth callback flow and enabling `auth.api.getAccessToken()` for BFF→API bearer forwarding.

## Context

- `packages/web/src/lib/auth.ts` currently calls `Database('auth.db')` without importing `Database` — incomplete edit, code does not compile.
- `better-sqlite3@12.10.0` and `@types/better-sqlite3@7.6.13` are already in `packages/web/package.json` + `bun.lock`.
- `packages/web/auth.db` exists (created by the half-complete previous attempt); `*.db` / `*.db-shm` / `*.db-wal` are already gitignored at repo root.
- Better Auth accepts a `better-sqlite3` instance directly as the `database` option.
- The CLI looks for auth config at `./src/lib/auth.ts` from cwd, then applies schema with `migrate`.
- `packages/web/src/lib/api.server.ts` already calls `auth.api.getAccessToken({ providerId: 'microsoft' })`. This requires account storage in DB (i.e. `account.storeAccountCookie: false`, which the current config already sets).
- A stale memory note claims the fix was `bun:sqlite`; the dev script runs Vite via Node (`node node_modules/vite/bin/vite.js`), so that wouldn't have worked at runtime. The right call is `better-sqlite3`.

## Files

- `packages/web/src/lib/auth.ts` — **modify**:
  - Import `Database from 'better-sqlite3'`.
  - Replace the broken `Database('auth.db')` line with a properly imported instance.
  - Add `socialProviders.microsoft.mapProfileToUser` to anchor `user.id` on Entra `oid` and pass through `name`/`email`.
- `packages/web/auth.db` — **created/migrated by Better Auth CLI** (not hand-edited).
- `~/.claude/projects/C--Users-PieterBracke-git-tsz/memory/project_oauth_session_null_bug.md` — **update**: correct `bun:sqlite` → `better-sqlite3` so future sessions don't get misled.

No other files change. `api.server.ts`, route guards, root layout, and the C# API remain as-is.

## Steps

1. **Fix `auth.ts` import + instantiation.** Replace:
   ```ts
   import { memoryAdapter } from 'better-auth/adapters/memory';  // remove
   const memoryDb = { ... };                                     // remove
   ```
   with:
   ```ts
   import Database from 'better-sqlite3';

   const sqlite = new Database('auth.db');
   sqlite.pragma('journal_mode = WAL');
   ```
   Then pass `database: sqlite` to `betterAuth({ ... })`. CWD-relative path is fine: both `bun --filter web` and the Better Auth CLI run from `packages/web`.

2. **Add `mapProfileToUser` on the Microsoft provider** so `user.id` is the Entra Object ID (per `login/plan.md` edge case). Inside `socialProviders.microsoft`:
   ```ts
   mapProfileToUser: (profile) => ({
     id: profile.oid ?? profile.sub,
     email: profile.email ?? profile.preferred_username,
     name: profile.name,
   }),
   ```
   `oid` is the stable per-tenant identifier; falling back to `sub` keeps it safe. `preferred_username` is a sensible email fallback because Entra often omits `email` for managed users.

3. **Delete the existing partially-written `auth.db`** before the first migrate, so we start from a clean schema. Reversible (Git ignores the file; no real users yet).

4. **Run the Better Auth CLI migrate from `packages/web/`:**
   ```bash
   bun --filter web exec npx @better-auth/cli@latest migrate
   ```
   This reads `src/lib/auth.ts`, generates the `user` / `session` / `account` / `verification` tables, and applies them to `auth.db`. The CLI evaluates `auth.ts`, so `.env.local` must be loadable.

5. **Smoke-test the golden path:**
   - `bun --filter web dev`
   - Visit `https://localhost:3000` → auto-redirects to Microsoft sign-in → consent → returns to `/`
   - Confirm session persists across refresh (proves DB read works after SSR module reload)
   - Click into `/animals` and confirm the BFF→API bearer-forwarded fetch succeeds (proves `getAccessToken` works against DB-backed account)
   - `sqlite3 packages/web/auth.db ".tables"` to confirm tables exist; `.schema session` to spot-check shape

6. **Update the stale memory note** to reflect that the fix is `better-sqlite3` (not `bun:sqlite`) and that CLI migrate is required.

## Tests

No automated tests added — manual smoke-test in step 5 covers the integration. The TanStack/React test setup in this repo doesn't currently mock Better Auth, and adding a DB-mocked unit test would test the mock, not the real flow.

## Edge Cases

- **CLI evaluating `auth.ts` at migrate time** — `auth.ts` reads `process.env.MICROSOFT_*` at module load. Missing env vars will crash the CLI with a confusing error. Ensure `.env.local` is loaded (Better Auth CLI auto-loads `.env`; if it doesn't pick up `.env.local`, prefix with `dotenv -e .env.local -- npx ...`).
- **`auth.db` CWD assumption** — relative path resolves against cwd. Works because dev and the CLI both run from `packages/web`. If we ever invoke from elsewhere (e.g. a root-level script), the file will be created in the wrong place. Revisit if that happens.
- **WAL files on Windows** — `journal_mode=WAL` creates `auth.db-shm` and `auth.db-wal`. Both are already gitignored.
- **Re-running migrate after plugin changes** — the BA CLI is idempotent for additive changes, but if we add plugins later (e.g. organization, 2FA), re-run migrate. Not part of this plan.
- **Schema drift between dev and prod** — eventually we'll want generated SQL files committed to git rather than `migrate`'s implicit DDL. Out of scope; flagged for later.
- **Existing partial `auth.db`** — created by the broken `Database('auth.db')` call before this fix. Deleting in step 3 is safe; no real user data yet.

## Assumptions

- We keep SQLite for the foreseeable future.
- The dev environment will keep running Vite via Node, not Bun (current `dev` script).
- `.env.local` in `packages/web/` already has the Microsoft client env vars populated, since OAuth was reportedly working in-session after the partial fix.
- We're not adding `compact`/`jwt` cookie-cache strategies on top of the DB session — keeping `session.cookieCache.enabled: false` so every request reads the DB. Cheap on SQLite; revisit if it shows up in profiles.

## Follow-up

- After implementation, update `docs/product/requirements/login/plan.md` to reflect that the BFF runs in stateful (DB-backed) mode rather than stateless cookie mode, and remove the stateless-cookie-budget edge case.
