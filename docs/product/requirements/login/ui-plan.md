# Plan: Login UI & FE–BFF session

## Goal

Wire the browser-facing login flow so that after a Microsoft Entra authentication, a session cookie is established between the browser (FE) and the TanStack Start BFF, and protected routes are gated behind that session.

## Relationship to `plan.md`

`plan.md` owns the BFF/backend side: betterAuth config, Microsoft provider, the `/api/auth/$` handler, the `api.server.ts` BFF→API client, and the C# JWT validation. This document owns the browser-facing layer: what the user sees, which routes exist, how session data flows into the UI, and how we phase from stateless cookie to a DB-backed user.

## What Microsoft provides (non-UI)

The Microsoft Entra login UI is hosted entirely by Microsoft. Our UI triggers a redirect; the user logs in on Microsoft's pages, and Microsoft calls back to `/api/auth/callback/microsoft` on the BFF. We control nothing on those Microsoft pages. The redirect URI for that callback is already registered in the app registration.

## Session contract: FE ↔ BFF

```
Browser                         BFF (TanStack Start)
  │                                     │
  │──── GET / (or any route) ───────────►│
  │                                     │ beforeLoad: getSession()
  │◄─── 200 RootLayout (no UI rendered) │ (no cookie → useEffect fires)
  │                                     │
  │──── XHR signIn.social('microsoft') ─►│
  │◄─── 302 → Microsoft login page ─────│ BFF initiates OIDC redirect
  │                                     │
  │──── [authenticate on Microsoft] ────►│
  │◄─── 302 /api/auth/callback/microsoft │ Microsoft redirects back
  │                                     │ betterAuth validates, creates session
  │◄═══ Set-Cookie: better-auth.session ═│ cookie scoped to BFF origin
  │                                     │
  │──── GET /original (with cookie) ────►│
  │                                     │ beforeLoad: getSession() ✓
  │◄─── 200 protected page ─────────────│
```

The session cookie is:
- `HttpOnly` — browser JS never reads it
- `Secure` — HTTPS only
- `Path=/`
- `SameSite=Lax` (Better Auth's default) — required so the cookie survives the cross-site redirect chain `microsoft.com → /api/auth/callback/microsoft → /`. Strict was tried and rolled back; see `plan.md` → **Cookie Security**.
- Signed/encrypted by betterAuth using `BETTER_AUTH_SECRET`
- **No `__Host-` prefix**. It was tried, then rolled back because it's incompatible with BA's path-scoped OAuth state cookies (`__Host-` requires `Path=/`, BA scopes them to `/api/auth/...`).

OAuth state and PKCE cookies share the same `Lax` default; nothing special to configure for them.

## Current — DB-backed (better-sqlite3, no roles yet)

betterAuth stores sessions, accounts, and refresh tokens in `packages/web/auth.db` (better-sqlite3, WAL). `getSession()` returns:

```ts
{
  user: {
    id: string        // Entra oid (via mapProfileToUser)
    name: string
    email: string
    image?: string
  }
  session: { ... }   // betterAuth internals
}
```

User data is sourced from Entra ID token claims at login and persisted in the `user` table; the `account` table also holds the Microsoft access + refresh tokens that `api.server.ts` retrieves via `auth.api.getAccessToken({ providerId: 'microsoft' })` to forward as Bearer on every BFF→API call.

Stateless cookie mode was the original plan; it was abandoned because (a) `getAccessToken` needs a DB-backed `account` row, and (b) packed Entra tokens risk overflowing browser cookie limits.

## Future — roles & app-specific user data

When we need roles, preferences, or audit trails, we extend the `user` table or add side tables. The `getSession()` interface stays identical — the consumer-facing shape just grows:

```ts
{
  user: {
    id: string
    name: string
    email: string
    image?: string
    role: 'admin' | 'user' | 'client_manager'   // ← from DB
  }
  session: { ... }
}
```

No UI changes required for the transition; route guards and nav components consume the same shape.

## Routes & components

### `src/routes/__root.tsx` — root layout (modify)

- When no session: `useEffect` auto-fires `authClient.signIn.social({ provider: 'microsoft', callbackURL: '/' })`; the layout renders `null` while the redirect happens. There is no manual Login button — the app is fully gated, so an unauthenticated user can never see UI.
- When session exists: render **"Signed in as {user.name} · Sign out"** in the nav bar. "Sign out" calls `authClient.signOut()` and then `window.location.assign('/')` to re-trigger the auto-redirect.
- No `/login` route exists.

### `src/routes/_protected.tsx` — pathless layout

- `beforeLoad`: calls `getSession()`.
  - No session → `throw redirect({ to: '/', search: { redirect: location.pathname } })`.
  - Session exists → inject `{ user }` into route context.
- No visible UI — transparent layout that wraps all protected routes.

### Existing routes to move under `_protected/`

| Current path | New path |
|---|---|
| `src/routes/index.tsx` | `src/routes/_protected/index.tsx` |
| `src/routes/animals/index.tsx` | `src/routes/_protected/animals/index.tsx` |
| `src/routes/animals/$id.tsx` | `src/routes/_protected/animals/$id.tsx` |

`routeTree.gen.ts` regenerates automatically — do not edit it.

## What we need to implement (UI-specific steps)

These complement steps 5, 9, 10, 11, 13 from `plan.md`:

1. **`src/lib/auth-client.ts`** — `createAuthClient()` with `baseURL: '/'`. Exports `authClient` (used only in browser code: login button, sign-out link).
2. **`src/routes/_protected.tsx`** — `beforeLoad` session guard.
3. **Move existing routes** under `_protected/` directory.
4. **`src/routes/__root.tsx`** — auto-redirect via `useEffect` when no session; "Signed in as … · Sign out" in nav when session exists.

## Error & loading states

| State | Behaviour |
|---|---|
| Auth in-flight (redirect pending) | `RootLayout` returns `null` while `useEffect` fires the redirect — blank screen, momentary |
| `?error=*` on return from Microsoft | `console.error` the error value; no UI message for now |
| Session expired mid-session | `getSession()` returns null → `RootLayout` auto-redirects to Microsoft again |
| `signOut()` in-flight | Nav "Sign out" button stays clickable; on success `window.location.assign('/')` triggers the auto-redirect flow |

## Not in scope for this plan

- Role-based UI gating (Phase 2 concern; add to a separate plan when roles exist)
- Entra global sign-out (app-local cookie clear only, per `plan.md`)
- Impersonation UI — now scoped in its own plan (`impersonation/plan.md`): header "Impersonate" button, red impersonation banner, target picker. Not part of *this* login UI plan.
