# Plan: Entra ID login via betterAuth (BFF)

## Goal

Add Microsoft Entra ID login to the TanStack Start app using betterAuth in stateless mode, and forward the Entra access token from the BFF to the C# API as `Authorization: Bearer …`.

## Context

- `packages/web` is TanStack Start (BFF-capable via server routes + server functions). `packages/api` is a separate C# .NET 10 minimal API.
- betterAuth supports a **stateless mode** — no DB; session and account data are stored in signed/encrypted cookies. Fits the decision that Entra is the source of truth for users.
- TanStack Start integration is first-class: mount `auth.handler` at `/api/auth/$`, use the `tanstackStartCookies()` plugin, and use server functions inside route `beforeLoad` for guards.
- Microsoft provider needs `clientId`, `clientSecret`, `tenantId`. Single-tenant → pin `tenantId` to the Euricom tenant GUID.
- Current `packages/web/src/api/client.ts` is a **browser** `openapi-fetch` client. To attach a Bearer token, calls must go through the BFF, so API access moves to TanStack server functions.

> **Note:** `requirements/login/login.md` currently says "one app registration shared by both frontend and API". The decision in this plan is to use **two app registrations** (one for Web, one for API). The requirement doc should be updated to match.

## Architecture

```
Browser ──cookie──► TanStack Start (BFF) ──Bearer (Entra access token, aud=API)──► C# API
                          │
                          └── betterAuth (Microsoft provider, stateless cookie session)
```

- No new database.
- **Two Azure app registrations:**
  - **Web app reg** (confidential client) — holds clientId + client secret used by betterAuth. Has delegated permission on the API's exposed scope.
  - **API app reg** — exposes scope `api://<api-client-id>/access`. C# API validates JWTs whose `aud` = its own client ID.
- The Entra access token obtained on behalf of the user has `aud` = the API app reg. BFF forwards it as Bearer; C# validates via Microsoft.Identity.Web.
- Browser never sees the Entra access token.

## Files

### `packages/web` — new

- `src/lib/auth.ts` — betterAuth instance: Microsoft social provider, single-tenant, stateless config (`cookieCache` + `account.storeAccountCookie`), `tanstackStartCookies()` plugin.
- `src/lib/auth-client.ts` — `createAuthClient()` for browser-side `signIn.social({ provider: 'microsoft' })` and `signOut()`.
- `src/lib/auth.functions.ts` — `getSession` and `ensureSession` server functions.
- `src/lib/api.server.ts` — server-only `openapi-fetch` client. Middleware reads the session, calls `auth.api.getAccessToken({ providerId: 'microsoft' })`, attaches `Authorization: Bearer …`.
- `src/routes/api/auth/$.ts` — mounts `auth.handler` for GET/POST (handles `/api/auth/callback/microsoft`, sign-in, sign-out, etc.).
- `src/routes/_protected.tsx` — pathless layout route. `beforeLoad` calls `getSession`; redirects to `/` when missing. Exposes `user` via route context.
- `.env.example` — committed template; documents all variables with placeholder values. See **Configuration & secrets** below.
- `.env.local` — **gitignored**, real values for local dev.

### `packages/web` — modify

- `src/routes/index.tsx` → move to `src/routes/_protected/index.tsx`.
- `src/routes/animals/index.tsx` → `src/routes/_protected/animals/index.tsx`.
- `src/routes/animals/$id.tsx` → `src/routes/_protected/animals/$id.tsx`.
- `src/api/animals.ts` — convert from direct browser fetches to TanStack server functions that use `api.server.ts`. Route loaders call those server functions.
- `src/routes/__root.tsx` — nav bar: when no session render a shadcn `Button` "Login" that calls `authClient.signIn.social({ provider: 'microsoft', callbackURL: currentPath })`; when session exists render "Signed in as {user.name} · Sign out".
- `package.json` — add `better-auth`.
- `src/api/client.ts` — keep schema types; stop exporting a browser-side client (or scope it to internal use from `api.server.ts`).

### `packages/api` — modify

- `api.csproj` — add `Microsoft.Identity.Web` package.
- `Program.cs` — `AddAuthentication().AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"))`, `AddAuthorization(o => o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())`, `app.UseAuthentication()`, `app.UseAuthorization()`. Add `.AllowAnonymous()` on `MapOpenApi`, `MapScalarApiReference`, and the root `MapGet("/")` so `bun --filter web gen:api` keeps working.
- Refactor `AnimalEndpoints.Map` to take `IEndpointRouteBuilder` and use a route group; current shape (`Map(WebApplication)`) doesn't compose with `.RequireAuthorization()` cleanly. The fallback policy makes this implicit, but the refactor still lands.
- `appsettings.json` — add an empty `AzureAd` section (so the config layering is wired up). Real values come from user-secrets in dev. See **Configuration & secrets** below.
- No `appsettings.Development.json` change required for auth values; use `dotnet user-secrets` instead.

## Configuration & secrets

> TenantId and ClientIds are **not secrets** per Microsoft — they're public identifiers. Only the Web client secret and `BFF_AUTH_SECRET` are real secrets. Storage choices below reflect that.

### Web (`packages/web`) — `.env.local` (gitignored)

```env
# Entra — Web app registration
MICROSOFT_TENANT_ID=<Euricom tenant GUID>
MICROSOFT_CLIENT_ID=<Web app reg client ID>
MICROSOFT_CLIENT_SECRET=<Web app reg client secret>   # real secret

# Entra — API app registration (used only to build the requested scope)
API_CLIENT_ID=<API app reg client ID>

# BFF
BFF_AUTH_SECRET=<random 32+ byte string>               # real secret, used to sign cookies
BFF_URL=http://localhost:3000

# Downstream API
API_URL=http://localhost:5204
```

- `MICROSOFT_*` names match betterAuth's official Microsoft provider docs; they're our choice, read explicitly inside `src/lib/auth.ts`.
- `BFF_AUTH_SECRET` maps to betterAuth's `secret` config option; `BFF_URL` maps to `baseURL`. Pass them explicitly in `src/lib/auth.ts` — betterAuth's auto-read only applies to its own `BETTER_AUTH_*` names.
- A committed `.env.example` mirrors this file with placeholder values.
- Generate `BFF_AUTH_SECRET` with `openssl rand -base64 32`.

### API (`packages/api`) — `dotnet user-secrets` (per-developer, outside the repo)

Init the secret store once (one-time prerequisite): `dotnet user-secrets init` in `packages/api/`. Then:

```bash
dotnet user-secrets set "AzureAd:Instance" "https://login.microsoftonline.com/"
dotnet user-secrets set "AzureAd:TenantId" "<Euricom tenant GUID>"
dotnet user-secrets set "AzureAd:ClientId" "<API app reg client ID>"
```

- Values live in `%APPDATA%\Microsoft\UserSecrets\<id>\secrets.json` (Windows). Never committed.
- `appsettings.json` keeps an empty `AzureAd` section to anchor config schema; runtime values come from user-secrets in dev.
- None of these are technically secrets — using user-secrets just avoids hard-coding tenant/client IDs into a committed config file.

### `.gitignore` additions

Create `.gitignore` at the repo root (or extend if one appears) with at least:

```
.env
.env.*
!.env.example
```

(Plus the usual `bin/`, `obj/`, `node_modules/`, etc. — out of scope here if they already exist elsewhere.)

### Production

Out of scope for this plan. Document only: in deployed environments, the same variable names are injected as process env vars (Web) and via `AzureAd__*` env vars or a vault provider (API). Pick the actual store when we deploy.

## Steps

1. **Azure setup (manual prerequisite, owner: Pieter)** — create **two** single-tenant app registrations:
   - **API app reg**: expose scope `api://<api-client-id>/access`; no redirect URIs.
   - **Web app reg**: platform `Web` with redirect `http://localhost:3000/api/auth/callback/microsoft`; create a client secret; grant delegated permission to the API app reg's scope.
   Capture tenant ID, Web clientId + secret, API clientId.
2. **Populate secrets** (see **Configuration & secrets**): create `packages/web/.env.local` and `packages/web/.env.example`; run `dotnet user-secrets init` and `set` in `packages/api/`. Add `.env*` to `.gitignore`.
3. Add `better-auth` to `packages/web` (`bun add better-auth -F web`).
4. Create `src/lib/auth.ts` with Microsoft provider, single-tenant, stateless cookie session, `tanstackStartCookies()`. Pass `secret: process.env.BFF_AUTH_SECRET` and `baseURL: process.env.BFF_URL` explicitly. Scopes: `['openid', 'profile', 'email', 'offline_access', 'api://<api-client-id>/access']` — note the API app reg's clientId here so the issued access token's `aud` is the API.
5. Create `src/lib/auth-client.ts`.
6. Create `src/routes/api/auth/$.ts` mounting the handler.
7. Create `src/lib/auth.functions.ts` with `getSession` / `ensureSession`.
8. Build `src/lib/api.server.ts` — `openapi-fetch` client with a middleware that calls `auth.api.getAccessToken({ providerId: 'microsoft', headers })` and sets `Authorization` on the outgoing request.
9. Create `src/routes/_protected.tsx`. `beforeLoad` → `getSession`, redirect to `/` if missing.
11. Move existing routes (`index.tsx`, `animals/*`) under `_protected/`. Let `routeTree.gen.ts` regenerate (do not hand-edit).
12. Convert `src/api/animals.ts` to TanStack server functions that call `api.server.ts`. Update the moved route components' loaders to use them.
13. Update `__root.tsx` nav: "Login" button when unauthenticated; "Signed in as {user.name} · Sign out" when session exists.
14. C# API: add `Microsoft.Identity.Web` package, wire JwtBearer in `Program.cs`, add `AzureAd` config (TenantId, ClientId = API app reg), set a `FallbackPolicy` that requires authenticated user, and explicitly `.AllowAnonymous()` on `MapOpenApi`, `MapScalarApiReference`, and `MapGet("/")`. Refactor `AnimalEndpoints.Map` to take an `IEndpointRouteBuilder` group.
15. Regenerate the OpenAPI client (`bun --filter web gen:api`) once the API requires auth — verify metadata endpoints stay anonymous and types are unchanged.
16. Smoke test the golden path in a browser: visit `/` → redirected to `/login` → click Microsoft → consent → back to `/` → animals page loads via BFF with Bearer.

## Edge Cases

- **Stateless cookie budget (known risk).** No Phase 0 spike. We're committing to stateless mode and accepting that the Entra access + refresh + id token, encrypted, may not fit in browser cookie limits (~4KB/cookie). **Fallback if it breaks during implementation:** add a small SQLite file in `packages/web` and switch betterAuth to its SQLite adapter. The rest of the plan is unaffected.
- **Token `aud` verification.** During implementation, inspect a real issued access token on jwt.io after login to confirm `aud` = API app reg client ID and `scp` includes `access`. If betterAuth's Microsoft provider doesn't pass our custom API scope through, we won't get an API-audience token and the C# API will 401. Fixable, but spot it early.
- **Token refresh.** Entra access tokens are ~1h. We request `offline_access` and trust betterAuth's stateless refresh to renew + re-sign the cookie. **Fallback:** on any `401` from the C# API, the server client clears the session and the BFF redirects to `/`.
- **Identity anchor.** Key users on Entra `oid` (Object ID), not `email`. Entra often omits `email` for managed users, and email is tenant-mutable. Configure `mapProfileToUser` so betterAuth uses `profile.oid` as the user id and treats email as a display field.
- **Logout = app-only.** `signOut()` clears the betterAuth cookie. We do **not** trigger Entra global sign-out — next sign-in is one click. (Shared-machine sign-out is not a v1 concern.)
- **CORS.** Browser → BFF is same-origin (`localhost:3000`); BFF → C# API is server-to-server. No CORS config needed.
- **`routeTree.gen.ts`** regenerates on `vite` start — never hand-edit. Route changes happen by moving files only.
- **`SERVER_URL` migration.** Current `api/client.ts` reads `process.env.SERVER_URL` at module load (browser bundle). After the move to `api.server.ts`, switch to a server-only `API_URL` env var; the browser bundle no longer needs the API URL at all.

