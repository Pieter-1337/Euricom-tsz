# Plan: Entra ID login via betterAuth (BFF)

## Goal

Add Microsoft Entra ID login to the TanStack Start app using betterAuth backed by a local SQLite file, and forward the Entra access token from the BFF to the C# API as `Authorization: Bearer …`.

## Context

- `packages/web` is TanStack Start (BFF-capable via server routes + server functions). `packages/api` is a separate C# .NET 10 minimal API.
- betterAuth runs against a local SQLite file (`packages/web/auth.db`) via `better-sqlite3`. Sessions, accounts, and refresh tokens persist across process restarts and Vite SSR module isolation — required because `auth.api.getAccessToken()` reads the stored Microsoft access token on every API call. Stateless cookie mode was the original plan but was abandoned: cookie size risk plus `getAccessToken` needs a DB-backed `account` row.
- TanStack Start integration is first-class: mount `auth.handler` at `/api/auth/$`, use the `tanstackStartCookies()` plugin, and use server functions inside route `beforeLoad` for guards.
- Microsoft provider needs `clientId`, `clientSecret`, `tenantId`. Single-tenant → pin `tenantId` to the Euricom tenant GUID.
- Current `packages/web/src/api/client.ts` is a **browser** `openapi-fetch` client. To attach a Bearer token, calls must go through the BFF, so API access moves to TanStack server functions.

> **Note:** `requirements/login/login.md` currently says "one app registration shared by both frontend and API". The decision in this plan is to use **two app registrations** (one for Web, one for API). The requirement doc should be updated to match.

## Architecture

```
Browser ──cookie──► TanStack Start (BFF) ──Bearer (Entra access token, aud=API)──► C# API
                          │
                          └── betterAuth (Microsoft provider, better-sqlite3 session store)
```

- No new database.
- **Two Azure app registrations:**
  - **Web app reg** (confidential client) — holds clientId + client secret used by betterAuth. Has delegated permission on the API's exposed scope.
  - **API app reg** — exposes scope `api://<api-client-id>/access`. C# API validates JWTs whose `aud` = its own client ID.
- The Entra access token obtained on behalf of the user has `aud` = the API app reg. BFF forwards it as Bearer; C# validates via Microsoft.Identity.Web.
- Browser never sees the Entra access token.

## Files

### `packages/web` — new

- `src/lib/auth.ts` — betterAuth instance: Microsoft social provider, single-tenant, SQLite-backed via `new Database('auth.db')` with WAL journal mode, `tanstackStartCookies()` plugin. **Two non-obvious provider fields** must be set together: `scope` (singular array — BA's `socialProviders.microsoft` reads `scope`, **not** `scopes`; a `scopes` typo silently drops the entry) and `disableDefaultScope: true` (suppresses BA's hardcoded `User.Read` default — otherwise the token request mixes Graph + your API, Microsoft drops the API scope, and you get a Graph token instead of an API access token). Cookie config uses BA's relaxed defaults (`Lax`, no `__Host-`) per the cross-site OAuth redirect constraint — see **Cookie Security** below.
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
- `appsettings.json` — `AzureAd` section pre-fills `Instance: "https://login.microsoftonline.com/"` (a public constant) and leaves `TenantId` / `ClientId` blank. Real (non-secret) IDs come from `dotnet user-secrets` in dev. See **Configuration & secrets** below.
- `Program.cs` — in `Development`, attaches `JwtBearerEvents.OnAuthenticationFailed` and `OnTokenValidated` hooks that log the raw token, inner exception, and decoded `iss`/`aud`/`tid` to a `JwtDebug` logger. Pure diagnostics; the block is gated on `IsDevelopment()` so production never logs PII. Useful when a token validates against the wrong tenant or audience.
- No `appsettings.Development.json` change required for auth values; use `dotnet user-secrets` instead.

## Configuration & secrets

> TenantId and ClientIds are **not secrets** per Microsoft — they're public identifiers. Only the Web client secret and `BETTER_AUTH_SECRET` are real secrets. Storage choices below reflect that.

### Web (`packages/web`) — `.env.local` (gitignored)

```env
# Entra — Web app registration
MICROSOFT_TENANT_ID=<Euricom tenant GUID>
MICROSOFT_CLIENT_ID=<Web app reg client ID>
MICROSOFT_CLIENT_SECRET=<Web app reg client secret>   # real secret

# Entra — API app registration (used only to build the requested scope)
API_CLIENT_ID=<API app reg client ID>

# BFF
BETTER_AUTH_SECRET=<random 32+ byte string>               # real secret, used to sign cookies
BETTER_AUTH_URL=https://localhost:3000

# Downstream API — SSR-only target. HTTP for dev to sidestep Node's
# CA store (which doesn't trust mkcert). The browser never hits this
# URL directly; every API call goes via TanStack server functions.
API_URL=http://localhost:5204
```

- `MICROSOFT_*` names match betterAuth's official Microsoft provider docs; they're our choice, read explicitly inside `src/lib/auth.ts`.
- `BETTER_AUTH_SECRET` maps to betterAuth's `secret` config option; `BETTER_AUTH_URL` maps to `baseURL`. Pass them explicitly in `src/lib/auth.ts` — betterAuth's auto-read only applies to its own `BETTER_AUTH_*` names.
- A committed `.env.example` mirrors this file with placeholder values.
- Generate `BETTER_AUTH_SECRET` with `openssl rand -base64 32`.

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
   - **Web app reg**: platform `Web` with redirect `https://localhost:3000/api/auth/callback/microsoft`; create a client secret; grant delegated permission (and admin-consent) to the API app reg's `access` scope.
   Capture tenant ID, Web clientId + secret, API clientId.
2. **Populate secrets** (see **Configuration & secrets**): create `packages/web/.env.local` and `packages/web/.env.example`; run `dotnet user-secrets init` and `set` in `packages/api/`. Add `.env*` to `.gitignore`.
3. Add `better-auth` to `packages/web` (`bun add better-auth -F web`).
4. Create `src/lib/auth.ts` with Microsoft provider, single-tenant, SQLite-backed (`new Database('auth.db')` from `better-sqlite3`, WAL journal mode), `tanstackStartCookies()`. Pass `secret: process.env.BETTER_AUTH_SECRET` and `baseURL: process.env.BETTER_AUTH_URL` explicitly. **Scope config (gotchas — see Edge Cases):** use the **`scope`** field (singular array), include `['openid', 'profile', 'email', 'offline_access', 'api://<api-client-id>/access']`, and set **`disableDefaultScope: true`** to suppress BA's hardcoded `User.Read` default. Apply the **Cookie Security** config in `advanced` (see below). Then run the BA CLI migration to create tables: `cd packages/web && bunx @better-auth/cli migrate -y`.
5. Create `src/lib/auth-client.ts`.
6. Create `src/routes/api/auth/$.ts` mounting the handler.
7. Create `src/lib/auth.functions.ts` with `getSession` / `ensureSession`.
8. Build `src/lib/api.server.ts` — `openapi-fetch` client with a middleware that calls `auth.api.getAccessToken({ providerId: 'microsoft', headers })` and sets `Authorization` on the outgoing request.
9. Create `src/routes/_protected.tsx`. `beforeLoad` → `getSession`, redirect to `/` if missing.
11. Move existing routes (`index.tsx`, `animals/*`) under `_protected/`. Let `routeTree.gen.ts` regenerate (do not hand-edit).
12. Convert `src/api/animals.ts` to TanStack server functions that call `api.server.ts`. Update the moved route components' loaders to use them.
13. Update `__root.tsx`: when no session, auto-redirect via `useEffect` → `authClient.signIn.social({ provider: 'microsoft', callbackURL: '/' })` (no manual login button — the app is fully gated). When session exists, render "Signed in as {user.name} · Sign out" in the nav.
14. C# API: add `Microsoft.Identity.Web` package, wire JwtBearer in `Program.cs`, add `AzureAd` config (TenantId, ClientId = API app reg), set a `FallbackPolicy` that requires authenticated user, and explicitly `.AllowAnonymous()` on `MapOpenApi`, `MapScalarApiReference`, and `MapGet("/")`. Refactor `AnimalEndpoints.Map` to take an `IEndpointRouteBuilder` group.
15. Regenerate the OpenAPI client (`bun --filter web gen:api`) once the API requires auth — verify metadata endpoints stay anonymous and types are unchanged.
16. Smoke test the golden path in a browser: visit `/` → auto-redirected to Microsoft sign-in → consent → back to `/` → `/animals` page loads via BFF with Bearer. Open `packages/web/auth.db` and decode `account.access_token` at jwt.ms to confirm `aud` = API client id, `scp` includes `access`. The C# API's `JwtDebug` info log should fire on each successful validation.

## Cookie Security

Current state: `Secure`, `HttpOnly`, `Path=/` baseline on all cookies; `SameSite` left at Better Auth's default (`Lax`); **no `__Host-` prefix**.

The original plan was to harden with `__Host-` and `SameSite=Strict` on the session token. That was attempted, then rolled back, for two reasons documented inline in `src/lib/auth.ts`:

1. **`__Host-` prefix is incompatible with BA's path-scoped OAuth state cookies.** BA writes the state/PKCE cookies with `Path=/api/auth/...`; `__Host-` requires `Path=/`, so the combination breaks the callback flow.
2. **`SameSite=Strict` on the session token strips it on the post-OAuth redirect chain.** `microsoft.com → /api/auth/callback/microsoft → /` is cross-site for the whole chain per the SameSite spec, so a Strict cookie is dropped on the first `/` request and the route guard sees a null session. `Lax` (BA default) survives.

Effective config in `src/lib/auth.ts`:

```ts
advanced: {
  // cookiePrefix: '__Host-timesheetzone',   // intentionally disabled — breaks OAuth state cookies
  defaultCookieAttributes: {
    secure: true,
    httpOnly: true,
    path: '/',
    // sameSite intentionally omitted — Better Auth defaults to Lax, required for the OAuth redirect chain
  },
},
```

Trade-off: subdomain cookie injection is no longer blocked at the prefix level. Acceptable for the current setup because we don't share an eTLD+1 with any third party. Revisit if the deployment topology changes.

## Edge Cases

- **`scope` vs `scopes` field-name pitfall.** Better Auth's `socialProviders.microsoft` reads `scope` (singular array). Writing `scopes:` silently drops the entry — the original config did this and the API scope never reached the authorize URL, so the issued token had `aud = Microsoft Graph` not the API. The C# API then failed signature validation because Graph tokens are signed by Microsoft's internal key set (kid can collide with tenant keys by coincidence). Lesson: assert the field name from the TS types, not from a docs example.
- **`disableDefaultScope` is mandatory.** BA's Microsoft provider hardcodes `["openid", "profile", "email", "User.Read", "offline_access"]` as defaults and merges your scopes on top. `User.Read` is a Graph scope; combined with the API scope it makes the token request span two resources, and Microsoft v2 silently drops one. Setting `disableDefaultScope: true` keeps the request scoped to a single resource (the API).
- **Token `aud` verification.** After login, decode the access token stored in `account.access_token` (or paste into jwt.ms) and confirm `aud` = API app reg client ID, `scp` includes `access`, and `iss` is the v2 endpoint (`https://login.microsoftonline.com/{tid}/v2.0`) if you've flipped the API manifest's `accessTokenAcceptedVersion: 2`. The dev-only `JwtDebug` logger on the API logs `iss`/`aud`/`tid` on every successful validation as a sanity check.
- **Token refresh.** Entra access tokens are ~1h. We request `offline_access` and trust betterAuth's stateless refresh to renew + re-sign the cookie. **Fallback:** on any `401` from the C# API, the server client clears the session and the BFF redirects to `/`.
- **Identity anchor.** Key users on Entra `oid` (Object ID), not `email`. Entra often omits `email` for managed users, and email is tenant-mutable. Configure `mapProfileToUser` so betterAuth uses `profile.oid` as the user id and treats email as a display field.
- **Logout = app-only.** `signOut()` clears the betterAuth cookie. We do **not** trigger Entra global sign-out — next sign-in is one click. (Shared-machine sign-out is not a v1 concern.)
- **CORS.** Browser → BFF is same-origin (`localhost:3000`); BFF → C# API is server-to-server. No CORS config needed.
- **`routeTree.gen.ts`** regenerates on `vite` start — never hand-edit. Route changes happen by moving files only.
- **`SERVER_URL` migration.** Current `api/client.ts` reads `process.env.SERVER_URL` at module load (browser bundle). After the move to `api.server.ts`, switch to a server-only `API_URL` env var; the browser bundle no longer needs the API URL at all.

