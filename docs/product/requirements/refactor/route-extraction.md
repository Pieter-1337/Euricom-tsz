# Plan: Extract users routes into `src/features/users/`

## Goal

Move users-feature UI, server functions, and zod schemas out of route files and into a `src/features/users/` folder so each users route becomes a thin composer that only handles route definition + composition.

## Context

Audit of `packages/web/src/routes/` (8 files):

| Route | Lines | Verdict |
|---|---|---|
| `__root.tsx` | 70 | Modest — out of scope |
| `_protected.tsx` | 94 | Modest — out of scope |
| `_protected/index.tsx` | 12 | Trivial — leave |
| `_protected/admin.tsx` | 10 | Trivial gate — leave |
| `_protected/admin/users/index.tsx` | 117 | Medium — extract |
| `_protected/admin/users/new.tsx` | 87 | Medium — extract |
| `_protected/admin/users/$id.tsx` | **255** | Heavy — extract (biggest win) |
| `no-access.tsx` | 24 | Trivial — leave |

Existing patterns:

- Shared UI in `src/components/<category>/` (`list/`, `form/`, `ui/`)
- API types in `src/api/*.ts`, API client callers in `src/api/*.server.ts`
- No feature folder convention exists yet — `src/features/` is new
- Server fns + zod schemas are currently inlined in route files — main source of bloat

## Files

- `packages/web/src/features/users/schemas.ts` — **create** (zod schemas + `UserSortKey` type)
- `packages/web/src/features/users/server-fns.ts` — **create** (`createServerFn` wrappers)
- `packages/web/src/features/users/components/users-list.tsx` — **create**
- `packages/web/src/features/users/components/user-create-form.tsx` — **create**
- `packages/web/src/features/users/components/user-edit-card.tsx` — **create**
- `packages/web/src/features/users/components/leave-overview-section.tsx` — **create**
- `packages/web/src/routes/_protected/admin/users/index.tsx` — **modify** (thin)
- `packages/web/src/routes/_protected/admin/users/new.tsx` — **modify** (thin)
- `packages/web/src/routes/_protected/admin/users/$id.tsx` — **modify** (thin)

### Target folder layout

```
src/features/users/
  schemas.ts
  server-fns.ts
  components/
    users-list.tsx
    user-create-form.tsx
    user-edit-card.tsx
    leave-overview-section.tsx

routes/_protected/admin/users/
  index.tsx   (thin)
  new.tsx     (thin)
  $id.tsx     (thin)
```

## Steps

1. Create `src/features/users/schemas.ts`. Export named:
   - `userIdSchema`
   - `createUserSchema`
   - `updateUserSchema`
   - `saveUserInputSchema`
   - `leavesFormSchema`
   - `getUsersPagedParamsSchema`
   - `UserSortKey` type (`'name' | 'email' | 'role'`)
2. Create `src/features/users/server-fns.ts`. Move these six `createServerFn` wrappers from the route files:
   - `fetchUsersPaged` (from `users/index.tsx`)
   - `submitCreateUser` (from `users/new.tsx`)
   - `fetchUserAndLeaves` (from `users/$id.tsx`)
   - `saveUser` (from `users/$id.tsx`)
   - `deleteUser` (from `users/$id.tsx`)
   - `submitUpdateUserLeaves` (from `users/$id.tsx`)
   Imports from `#/api/users.server`, `#/api/user-leaves.server`, `#/features/users/schemas`.
3. Create `src/features/users/components/users-list.tsx`. Move `UsersList` from `users/index.tsx`. Import `fetchUsersPaged` and `UserSortKey` from the new modules. Export `UsersList` as a named export.
4. Create `src/features/users/components/user-create-form.tsx`. Move `NewUser` from `users/new.tsx`. Rename export to `UserCreateForm`. Import `createUserSchema` and `submitCreateUser`.
5. Create `src/features/users/components/leave-overview-section.tsx`. Move `LeaveOverviewSection` from `users/$id.tsx`. Import `leavesFormSchema` and `submitUpdateUserLeaves`.
6. Create `src/features/users/components/user-edit-card.tsx`. Move the page header (H1 + Delete button) and the General form from `users/$id.tsx` into a single component that takes `user: User` as a prop. Import `updateUserSchema`, `saveUser`, `deleteUser`. Preserve current behavior where delete errors flow through the General form's `handleApiError`.
7. Reduce `routes/_protected/admin/users/index.tsx` to: import `UsersList`, define route with `component: UsersList`.
8. Reduce `routes/_protected/admin/users/new.tsx` to: import `UserCreateForm`, define route with `component: UserCreateForm`.
9. Reduce `routes/_protected/admin/users/$id.tsx` to: import `fetchUserAndLeaves`, `UserEditCard`, `LeaveOverviewSection`. Define route with `loader: ({ params }) => fetchUserAndLeaves({ data: params.id })` and a thin `EditUserPage` component that:
   - Calls `Route.useLoaderData()` to get `{ user, leaves }`
   - Handles the "user not found" branch
   - Renders `<UserEditCard user={user} />` and `<LeaveOverviewSection userId={user.id} leaves={leaves} />`
10. Run `bun --filter web typecheck` (or full build) to confirm no broken imports.
11. Run `bun --filter web dev` and smoke-test in the browser:
    - **List**: sort by name/email/role, search, toggle Active, click a row → navigates to detail
    - **New**: validation errors fire, successful create navigates to `/admin/users/$id`
    - **Edit**: detail loads, save General form, save Leave overview, Delete with confirm navigates back to list

## Tests

No existing UI tests for these routes — no test changes required. Manual smoke-test in step 11 covers regression.

## Edge cases

- **TanStack Start server-fn bundling**: `createServerFn` calls in `server-fns.ts` are imported by client components. This matches how the current route files work (TanStack Start splits server handlers at build time). Verify with the dev-server smoke test.
- **`useLoaderData` is route-scoped**: `Route.useLoaderData()` only works inside the route component. The loader stays on the route, and `EditUserPage` (still in the route file) calls `useLoaderData()`, then passes `user`/`leaves` down as props. Extracted components do not call `useLoaderData()`.
- **Delete error coupling**: current code routes delete errors through the General form's `handleApiError`. Preserved by keeping the Delete button inside `UserEditCard` alongside the form (not promoted to the page level).
- **"User not found" branch**: stays in the route file's `EditUserPage` so `UserEditCard` can assume `user` is non-null.
- **Re-renaming**: `NewUser` → `UserCreateForm` is a rename only; no behavior change.

## Assumptions

- `src/features/` is a new folder — no existing convention to follow. Routes-as-thin-composers is the desired pattern.
- Out of scope: `__root.tsx`, `_protected.tsx`, `_protected/admin.tsx`, `_protected/index.tsx`, `no-access.tsx`.
- API client files (`src/api/users.server.ts`, `src/api/user-leaves.server.ts`) stay put — already well-organized.
- Existing `src/api/users.ts` keeps its types (`User`, `UserRole`, `USER_ROLES`); only zod *form/validation* schemas move to the feature folder.
