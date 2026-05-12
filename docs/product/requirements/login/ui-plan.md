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
  │──── GET /any-protected-route ───────►│
  │                                     │ beforeLoad: getSession()
  │◄─── 302 /login?redirect=/original ──│ (no cookie → redirect)
  │                                     │
  │──── click "Login" (navbar) ─────────►│
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
- `SameSite=Strict` — never sent on cross-site requests (safe because it is only needed after login, on same-site navigations)
- `Secure` — HTTPS only
- `__Host-timesheetzone` prefix — binds cookie to exact origin, prevents subdomain injection
- Signed/encrypted by betterAuth using `BFF_AUTH_SECRET`

OAuth state and PKCE cookies use `SameSite=Lax` (Better Auth's default) so they survive the cross-site redirect from Microsoft back to the callback route.

## Phase 1 — stateless (better-auth, no DB)

betterAuth packs the Entra tokens + minimal user profile into the session cookie itself. `getSession()` returns:

```ts
{
  user: {
    id: string        // Entra oid
    name: string
    email: string
    image?: string
  }
  session: { ... }   // betterAuth internals
}
```

No database. No roles. User data comes directly from the Entra ID token claims.

## Phase 2 — DB-backed user (future)

When we need roles, preferences, or audit trails, we add a `users` table (SQLite in `packages/web`, same DB betterAuth can use). The `getSession()` interface stays identical — betterAuth reads from DB on each request, enriches the user object:

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

- When no session: render a **"Login"** shadcn `Button` in the nav bar. `onClick`: `authClient.signIn.social({ provider: 'microsoft', callbackURL: currentPath })`.
- When session exists: render **"Signed in as {user.name} · Sign out"** in the nav bar. "Sign out" calls `authClient.signOut()`.
- No separate `/login` route needed for now — the nav bar is the entry point.

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
4. **`src/routes/__root.tsx`** — Login button when unauthenticated; "Signed in as … · Sign out" when session exists.

## Error & loading states

| State | Behaviour |
|---|---|
| Auth in-flight (redirect pending) | Button shows spinner, disabled |
| `?error=*` on return from Microsoft | `console.error` the error value; no UI message for now |
| Session expired mid-session | `getSession()` returns null → `_protected.tsx` redirects to `/` |
| `signOut()` in-flight | Nav link shows spinner |

## Not in scope for this plan

- Role-based UI gating (Phase 2 concern; add to a separate plan when roles exist)
- Entra global sign-out (app-local cookie clear only, per `plan.md`)
- Impersonation UI (listed as optional in `login.md`)
