# Implementation Status: Entra ID Login

_Last updated: 2026-05-11_

## Overall Status: In Progress (13/16 steps done, 2 blockers)

---

## Steps

| # | Step | Status | Notes |
|---|------|--------|-------|
| 1 | Azure app registrations (manual) | ✅ Done | Two app regs created; `.env.local` present |
| 2 | Populate secrets + env files | ✅ Done | `packages/web/.env.local` + `.env.example` created; `dotnet user-secrets` set; `.gitignore` updated |
| 3 | `bun add better-auth` | ⚠️ Partial | Package installed in bun cache + lockfile, but added to root `package.json` instead of `packages/web/package.json` (see Blocker 1) |
| 4 | Create `src/lib/auth.ts` | ✅ Done | Uses memory adapter, `tanstackStartCookies()`, Microsoft provider |
| 5 | Create `src/lib/auth-client.ts` | ✅ Done | `createAuthClient()` from `better-auth/client` |
| 6 | Create `src/routes/api/auth/$.ts` | ✅ Done | `server.handlers` pattern (no `createAPIFileRoute` in this version) |
| 7 | Create `src/lib/auth.functions.ts` | ✅ Done | `getSession` server fn returns `{ user: { id, name, email } } \| null` |
| 8 | Create `src/lib/api.server.ts` | ✅ Done | Bearer middleware via `auth.api.getAccessToken` + `getRequest()` |
| 9 | Create `src/routes/login.tsx` | ✅ Done | Shadcn Button, redirect validation, `beforeLoad` already-logged-in check |
| 10 | Create `src/routes/_protected.tsx` | ✅ Done | Pathless layout, reads session from root context, redirects to `/login` |
| 11 | Move routes under `_protected/` | ✅ Done | `index.tsx`, `animals/index.tsx`, `animals/$id.tsx` moved; old files deleted |
| 12 | Convert `animals.ts` to use `api.server.ts` | ✅ Done | Import switched from `client.ts` to `api.server.ts` |
| 13 | Add auth affordance in `__root.tsx` | ✅ Done | `beforeLoad` calls `getSession`; nav shows "Name · Sign out" when session exists |
| 14 | C# API: Microsoft.Identity.Web + JWT | ✅ Done | Package added, `Program.cs` wired, fallback policy, `.AllowAnonymous()` on meta endpoints, `AnimalEndpoints` now takes `IEndpointRouteBuilder`; builds clean |
| 15 | Regenerate OpenAPI client | ❌ Blocked | Needs the API running (`http://localhost:5204`) + Vite blocker resolved first |
| 16 | Smoke test | ❌ Not started | Manual step |

---

## Blockers

### Blocker 1 — `better-auth` in wrong `package.json`

`bun add better-auth --filter web` matched the filter against the root workspace (name `template-dotnet-spa-react`) instead of `packages/web` (name `web-tanstack-start`), so `better-auth` was added to the **root** `package.json` instead of `packages/web/package.json`.

**Fix:**
```bash
# Remove from root package.json manually (edit the file)
# Then add to the correct workspace:
bun add better-auth --filter web-tanstack-start
```
Or just edit `packages/web/package.json` directly:
```json
"better-auth": "^1.6.10",
```
Then run `bun install`.

### Blocker 2 — `h3-v2` package not resolved (pre-existing)

The Vite dev server fails to start with:
```
Error [ERR_MODULE_NOT_FOUND]: Cannot find package 'h3-v2'
imported from @tanstack/start-server-core/.../request-response.js
```

This is **pre-existing** (confirmed: same error on the pre-change commit). `h3-v2` is an npm alias (`npm:h3@2.0.1-rc.20`) declared as a dependency of `@tanstack/start-server-core`. Bun stores it in its cache but doesn't create the `node_modules/h3-v2` symlink that Node.js needs when Vite loads.

**Fix:** Add the alias explicitly to `packages/web/package.json` so bun creates the proper symlink:
```json
"h3-v2": "npm:h3@2.0.1-rc.20"
```
Then run `bun install`.

Once Blocker 2 is resolved, the dev server will start and:
- `routeTree.gen.ts` will auto-regenerate (Vite plugin handles this)
- TypeScript errors from stale route tree will clear
- Step 15 (gen:api) can run against the live API

---

## Key Design Decisions Made

| Decision | What was chosen |
|----------|----------------|
| No DB for betterAuth | Used `memoryAdapter` from `better-auth/adapters/memory` — data resets on restart, acceptable since Entra is source of truth. SQLite fallback still available. |
| API route pattern | `createFileRoute('/api/auth/$')` with `server.handlers` — `createAPIFileRoute` does not exist in `@tanstack/react-start@1.167.65` |
| Session in root context | Root route's `beforeLoad` calls `getSession()` once; all child routes read from `context.session`. `_protected.tsx` redirects if null. |
| `getRequest()` for incoming headers | `getRequest` from `@tanstack/react-start/server` (not `getWebRequest`) — correct name in this version |
| betterAuth env var names | `.env.local` uses `BETTER_AUTH_SECRET`/`BETTER_AUTH_URL` (betterAuth native names) — betterAuth auto-reads these |

---

## Files Changed / Created

### New files
- `packages/web/src/lib/auth.ts`
- `packages/web/src/lib/auth-client.ts`
- `packages/web/src/lib/auth.functions.ts`
- `packages/web/src/lib/api.server.ts`
- `packages/web/src/routes/api/auth/$.ts`
- `packages/web/src/routes/login.tsx`
- `packages/web/src/routes/_protected.tsx`
- `packages/web/src/routes/_protected/index.tsx`
- `packages/web/src/routes/_protected/animals/index.tsx`
- `packages/web/src/routes/_protected/animals/$id.tsx`
- `packages/web/.env.example`
- `packages/web/.env.local`

### Modified
- `packages/web/src/routes/__root.tsx` — `beforeLoad` + auth nav
- `packages/web/src/api/animals.ts` — uses `apiClient` from `api.server.ts`
- `packages/web/src/api/client.ts` — removed browser client; kept `ApiRequestError`
- `packages/api/Program.cs` — JWT auth, fallback policy, `.AllowAnonymous()` on meta
- `packages/api/api.csproj` — added `Microsoft.Identity.Web`
- `packages/api/appsettings.json` — added empty `AzureAd` section
- `packages/api/Modules/Animals/AnimalEndpoints.cs` — `IEndpointRouteBuilder` param
- `.gitignore` — added `.env.*` / `!.env.example`

### Deleted (moved)
- `packages/web/src/routes/index.tsx` → `_protected/index.tsx`
- `packages/web/src/routes/animals/index.tsx` → `_protected/animals/index.tsx`
- `packages/web/src/routes/animals/$id.tsx` → `_protected/animals/$id.tsx`

---

## Next Steps (to resume)

1. Fix Blocker 1: move `better-auth` to `packages/web/package.json`
2. Fix Blocker 2: add `"h3-v2": "npm:h3@2.0.1-rc.20"` to `packages/web/package.json`, then `bun install`
3. Start dev server — `routeTree.gen.ts` will regenerate automatically
4. Verify TypeScript passes: `bun run --cwd packages/web typecheck`
5. Start C# API, then run `bun run --cwd packages/web gen:api` (Step 15)
6. Smoke test the golden path (Step 16)
