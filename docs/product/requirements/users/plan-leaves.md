# Plan: Leaves — `LeaveType` (seeded) + per-user-per-year `UserLeave`

## Goal

Replace the four flat decimal columns on `User` (`HolidayDays / AdvDays / AncienniteitDays / SicknessDays`) with:

- A small fixed `LeaveType` reference list, seeded once at startup. Not admin-managed.
- A per-user × leave-type × year `UserLeave` row that stores **only** the admin-configured `TotalDays`.
- A `UserLeaveDto` that surfaces `year`, `totalDays`, `takenDays`, `balanceDays` to the frontend — `takenDays` and `balanceDays` are computed by the future timesheets module and stay `null` until then.

The admin uses this on the user edit page to set numbers per leave type per year. The numbers feed the leave-overview / timesheet flow when that module lands.

## Non-goals (explicit)

- **No admin CRUD on `LeaveType`.** The list is seeded and immutable from the UI. No `/admin/leave-types` page, no POST/PUT/DELETE endpoints. If the catalogue needs to grow, that's a separate plan (new seed, new migration if any).
- **No `Add leave` / `Delete leave` buttons** on the user edit page. The (user × leaveType × year) tuple is implicit — every combination addressable, materialized lazily.
- **No calendar UI on the user detail page.** The leave-overview calendar mockup (12-month grid + Balance panel) is a separate plan, tracked under `requirements/leave-overview/leave-overview.md`. This plan only ships the *amounts* admin can set.
- **No `Allowed` per UserLeave.** Whether a leave type is `Limited` / `Unlimited` is a property of the `LeaveType` itself (`DefaultAllowed`). Admin can't override it per user.
- **No `Taken` / `Balance` computation.** Both stay `null` in the DTO. Wiring the contract now means zero churn when the timesheet module fills them.
- **No tenancy / per-org leave types.**
- **No automatic per-year roll-over job.** Year rows materialize lazily on first GET (see below).
- **No back-fill of existing users on schema change.** Dev DB is wiped via `EnsureCreated`; no prod data.

## Data model

### `LeaveType` (new — `Modules/Users/`)

Immutable seeded reference data. No soft-delete. Lives in `Modules/Users/` as a **supporting entity** of the user-leaves slices — there is no `LeaveTypes` module and no standalone endpoint. The catalogue is implementation detail of the slices that join it into `UserLeaveDto`.

| Field            | Type           | Notes                                                                                |
| ---------------- | -------------- | ------------------------------------------------------------------------------------ |
| `Id`             | `Guid`         | PK.                                                                                  |
| `Name`           | `string`       | Required. Unique.                                                                    |
| `DefaultDays`    | `decimal?`     | Default total days. `null` for unlimited types. Used as the synthesized-row value when no `UserLeave` exists. |
| `DefaultAllowed` | `LeaveAllowed` | `Limited` or `Unlimited`. Drives whether the admin UI shows an editable Total field. |

EF config: plain unique index on `Name`. No query filter. Static `Create` factory. The 4 catalogue rows are seeded via `HasData()` in `LeaveTypeConfiguration` (baked into the migration, runs in **every** environment, not just Dev).

### `LeaveAllowed` enum (new — `Modules/Users/`)

```csharp
public enum LeaveAllowed { NotAllowed = 0, Limited = 1, Unlimited = 2 }
```

Stored as string via `HasConversion<string>()` (consistent with `UserRole`). `NotAllowed` is kept in the enum for forward compat but is **not used** by any seeded type today.

### `UserLeave` (new — `Modules/Users/`)

| Field         | Type       | Notes                                                                                    |
| ------------- | ---------- | ---------------------------------------------------------------------------------------- |
| `Id`          | `Guid`     | PK.                                                                                      |
| `UserId`      | `Guid`     | FK → `User.Id`. Cascade delete.                                                          |
| `LeaveTypeId` | `Guid`     | FK → `LeaveType.Id`. Restrict.                                                           |
| `Year`        | `int`      | Calendar year.                                                                           |
| `TotalDays`   | `decimal?` | Admin-configured override of `LeaveType.DefaultDays`. `null` when the type is Unlimited. |

Unique index on `(UserId, LeaveTypeId, Year)`. No soft-delete.

A row is **only created when admin explicitly saves a value** for that (user, leaveType, year). The absence of a row is meaningful: "use the default from `LeaveType.DefaultDays`."

### `User` (existing — schema diff)

Remove columns and consts:
- `HolidayDays`
- `AdvDays`
- `AncienniteitDays`
- `SicknessDays`
- All four `Default*Days` consts.
- The `User.Create` factory no longer seeds them.

## Seeding (all environments)

The 4 `LeaveType` rows are seeded via `modelBuilder.Entity<LeaveType>().HasData(...)` in `LeaveTypeConfiguration`. Single source of truth lives with the entity config; rows are baked into the migration and applied automatically on `db.Database.Migrate()`. No runtime seeder, no `IsDevelopment()` gate — the catalogue must exist in every environment or the leave-overview UI is permanently blank.

| Name           | DefaultAllowed | DefaultDays |
| -------------- | -------------- | ----------- |
| Verlof         | `Limited`      | `20`        |
| ADV dagen      | `Limited`      | `5`         |
| Anciënniteit   | `Limited`      | `0`         |
| Ziekte         | `Unlimited`    | `null`      |

`Id` values for `HasData()` rows are hard-coded deterministic `Guid`s in the config so the migration is reproducible.

## Lazy materialization — how GET works

`CreateUser` does **not** seed `UserLeave` rows. The grid is built on-the-fly by GET.

```
GET /api/users/{userId}/leaves?year={year}
  1. types ← all LeaveType rows.
  2. saved ← all UserLeave rows WHERE UserId = userId AND Year = year.
  3. For each leaveType in types:
       row ← saved.FirstOrDefault(s => s.LeaveTypeId == leaveType.Id)
              ?? synthesized {                             // not persisted
                   Id = Guid.Empty,
                   UserId = userId,
                   LeaveTypeId = leaveType.Id,
                   Year = year,
                   TotalDays = leaveType.DefaultDays
                 }
       yield UserLeaveDto.From(row, leaveType)
  4. Return the projected list. No DB writes.
```

This means:
- A brand-new user instantly has the full catalogue for any year — without `CreateUser` running a seeder.
- Navigating to year 2030 just works; the synthesized row uses today's `LeaveType.DefaultDays`.
- Bumping `Verlof.DefaultDays` from 20 → 22 retroactively changes any year the admin hasn't customized. **Acknowledged judgement call** — fine for now; harden later if frozen-per-year defaults are ever needed.

`UserLeaveDto` shape:

```jsonc
{
  "leaveTypeId": "...",
  "leaveTypeName": "Verlof",
  "defaultAllowed": "Limited",      // from joined LeaveType — drives UI render
  "year": 2026,
  "totalDays": 20,                  // synthesized from default, or persisted override
  "takenDays": null,                // always null until timesheets ship
  "balanceDays": null               // always null until timesheets ship
}
```

> Note: `id` is intentionally omitted from the DTO — synthesized rows have no real PK, and the admin's PUT (see below) is keyed on `(leaveTypeId, year)`, not `id`. Frontend never sees a `UserLeave.Id`.

## API surface

All endpoints require an authenticated Entra JWT. Admin gating per `AuthorizationPolicies.RequireAdmin` except where noted.

**No `LeaveType` endpoints.** No slice needs the catalogue as a standalone list — the user-leaves slices join `LeaveType` server-side and project name + `defaultAllowed` into `UserLeaveDto`. The future timesheets / leave-overview slices will do the same. If a future feature ever does need the standalone list (e.g. a filter dropdown), add a `GetLeaveTypes` slice then.

### UserLeaves

| Method | Path                                       | Auth                  | Body / Response                                                                                   |
| ------ | ------------------------------------------ | --------------------- | ------------------------------------------------------------------------------------------------- |
| GET    | `/api/users/{userId}/leaves?year={year}`   | JWT + `RequireAdmin`  | `200 UserLeaveDto[]` for that year — merged set, one per LeaveType. `year` defaults to current year. |
| PUT    | `/api/users/{userId}/leaves`               | JWT + `RequireAdmin`  | `UpsertUserLeaveCommand { leaveTypeId, year, totalDays? }` → `200 UserLeaveDto`. Upserts on `(userId, leaveTypeId, year)`. |

Only two endpoints. There is no POST (creating a row is implicit on first PUT) and no DELETE (admin can't remove a row — the (user, leaveType, year) tuple is conceptually permanent; setting `totalDays` to match `LeaveType.DefaultDays` is the closest thing to "reset to default").

### Validation (FluentValidation)

- `UpsertUserLeaveCommand`:
  - `LeaveTypeId`: required, must exist (handler-level check, not validator).
  - `Year`: between 2000 and 2100 (sanity range).
  - `TotalDays`: if the joined `LeaveType.DefaultAllowed == Unlimited`, must be `null`; if `Limited`, must be non-null and `>= 0`. The LeaveType-conditional check lives in the handler (it needs a DB lookup); the validator only checks shape (`TotalDays` ≥ 0 when set).

### Endpoint result mapping

`UpdateUserLeave` (better: `UpsertUserLeave`) handler returns a 3-way discriminated result so the endpoint can map to status codes:

| Result            | HTTP                                                          |
| ----------------- | ------------------------------------------------------------- |
| `NotFound`        | `404` — user or leave type doesn't exist.                     |
| `Invalid(reason)` | `400 ValidationProblem` — total/allowed mismatch.             |
| `Upserted(dto)`   | `200` — row created or updated.                               |

## CQRS slices (final file layout)

```
Modules/Users/
  LeaveType.cs                  ← supporting entity (no module of its own)
  LeaveAllowed.cs
  LeaveTypeConfiguration.cs     ← unique index on Name + HasData() seed for the 4 rows
  UserLeave.cs
  UserLeaveConfiguration.cs
  UserLeaveDto.cs               ← surface DTO; carries joined LeaveType fields
  Features/
    GetUserLeaves.cs            ← merged-set materializer, no DB writes
    UpsertUserLeave.cs          ← single upsert endpoint, replaces Add/Update/Delete
  (existing) CreateUser.cs — REVERT TimeProvider injection + UserLeave seeding loop; no longer seeds rows
```

No `Modules/LeaveTypes/` folder. No `LeaveTypeDto`, `LeaveTypeEndpoints`, `LeaveTypeSeeder`, or `Features/GetLeaveTypes.cs` — all dropped. The `Tsz.Api.Modules.LeaveTypes` namespace goes away; everything lives under `Tsz.Api.Modules.Users`.

`User.cs` schema diff (remove the 4 columns). `UserDto.cs` schema diff (remove the 4 columns).

## Migrations

Single migration, name suggestion: `LeavesModel`. It does:

1. Create `LeaveTypes` table (no `DeletedAt`).
2. Create `UserLeaves` table with FKs (`UserId` cascade, `LeaveTypeId` restrict). Unique index on `(UserId, LeaveTypeId, Year)`.
3. Drop `HolidayDays / AdvDays / AncienniteitDays / SicknessDays` from `Users`.

Dev DB is wiped via `EnsureCreated`; no prod data to preserve.

## Frontend (`packages/web`)

### Generated types

After API ships: `bun --filter web gen:api`. Schemas present: `LeaveAllowed`, `UserLeaveDto`, `UpsertUserLeaveCommand`. No `LeaveTypeDto` (no endpoint exposes it).

### API wrappers (`packages/web/src/api/`)

- `user-leaves.ts` + `user-leaves.server.ts` — `getUserLeaves(userId, year)`, `upsertUserLeave(userId, body)`. No add, no delete.

No `leave-types.ts` wrapper — frontend never calls a LeaveType endpoint; everything it needs is on `UserLeaveDto`.

`LeaveAllowed` const + type + values array, modelled on `UserRole` (matches the existing pattern; no magic strings at call sites).

### Routes

- `src/routes/_protected/admin/users/$id.tsx` — **modify**: add a "Leave overview" section below the existing General section.

No `/admin/leave-types/` routes. No nav link for leave types.

### "Leave overview" section on the admin user edit page

Layout (admin-facing, single grid + year nav, **no calendar**):

```
┌ Leave overview ──────────────────────────────────────────────┐
│ [< Prev]  2026  [Next >]                                     │
│                                                              │
│ Name           Allowed     Total  Taken  Balance             │
│ Verlof         Limited       20    —       —          [Edit] │
│ ADV dagen      Limited        5    —       —          [Edit] │
│ Anciënniteit   Limited        0    —       —          [Edit] │
│ Ziekte         Unlimited      —    —       —                 │
└──────────────────────────────────────────────────────────────┘
```

- Section title is **"Leave overview"**.
- Year navigation: prev / current-year-label / next arrows. Default to `new Date().getFullYear()`. Held in route-local `useState`.
- One row per LeaveType — every year, every user. Grid is never empty.
- `Taken` / `Balance` cells render `—` (DTO returns `null` for now).
- `Unlimited` rows: show "Unlimited" badge in the Allowed column, `—` in Total, **no Edit button**. (`Ziekte` is the only seeded Unlimited type.)
- `Limited` rows: show the value (from default or persisted override), Edit button visible.
- "Edit" opens a tiny shadcn `Dialog` with **one field**: `Total` (number, `>= 0`, required). Save → call `upsertUserLeave({ leaveTypeId, year, totalDays })`. Cancel → close.
- After save: refresh the local leaves state (or invalidate the route loader if we move loading there).

### Nav

No new nav entries. The Users link admins already see is enough — they access the Leave overview by drilling into a user.

## Tests

### Unit (`Tsz.Api.Tests`)

- `Builders/LeaveTypeBuilder.cs` (simple — `WithName`, `Limited(days)`, `Unlimited()`).
- `Builders/UserLeaveBuilder.cs` (`ForUser`, `ForType`, `Year`, `WithTotal`).
- `GetUserLeavesHandlerTests`:
  - Empty `UserLeaves` table → returns one synthesized row per `LeaveType` with `TotalDays = LeaveType.DefaultDays`.
  - Mix of persisted + missing → persisted values win, the rest synthesized.
  - Year switch returns the same shape but filtered to that year's persisted rows.
- `UpsertUserLeaveHandlerTests`:
  - Insert when no row exists → row created, returns `Upserted`.
  - Update when row exists → row updated.
  - Total mismatch with Unlimited type → `Invalid`.
  - Negative total → `Invalid` (validator).
  - Missing user / missing leaveType → `NotFound`.
- `UpsertUserLeaveValidatorTests` — shape rules (year range, `TotalDays >= 0` when set).
- `CreateUserHandlerTests` — assert that user-create no longer touches `UserLeave` (regression against the previous over-seeding implementation).

### Integration (`Tsz.Api.Tests.Integration`)

- `UserLeaveEndpointsTests`:
  - GET for a fresh user returns the full synthesized set (count == seeded LeaveType count).
  - PUT a Limited type → row persisted, subsequent GET shows the override.
  - PUT same key twice → updates, no duplicate row.
  - PUT for an Unlimited type with non-null total → 400.
  - Year filter: PUT for 2026, GET for 2027 → 2027 still shows defaults.
  - Non-admin → 403.
  - Soft-deleted user → 404 (no leak through query filter).

## Smoke test plan (manual, after implementation)

1. Sign in as seeded admin Pieter.
2. Open `/admin/users/$id` for Pieter → Leave overview section appears below General.
3. Default year is current year. Grid shows 4 rows: Verlof (20), ADV dagen (5), Anciënniteit (0), Ziekte (Unlimited — no Edit button).
4. Click Edit on Verlof → dialog opens with Total prefilled to 20. Change to 22 → Save. Grid reflects 22.
5. Click prev-year arrow → grid still shows 4 rows. Verlof is back to 20 (current year's override doesn't bleed). Edit Verlof for prev year, set 18 → Save. Grid shows 18.
6. Next-year arrow → forward to current year — Verlof still 22. Next-year again → 20 (synthesized default).
7. Try to manipulate Ziekte: no Edit button visible. Confirm via DB that no `UserLeave` row exists for Ziekte unless something went wrong.
8. Sign in as a non-admin → `/admin/users/$id` redirects to `/` (existing gate). API `PUT /api/users/.../leaves` returns 403.
9. Create a new user via admin UI → open their Leave overview → grid is full immediately with defaults. Confirm via DB that **zero** `UserLeave` rows exist for the new user (lazy materialization working).

## Steps

1. **API** — Collapse the LeaveType files into `Modules/Users/`: keep `LeaveType.cs` (no `DeletedAt`), `LeaveAllowed.cs`, `LeaveTypeConfiguration.cs` (plain unique index on Name + `HasData()` for the 4 catalogue rows with deterministic GUIDs). Delete `LeaveTypeDto.cs`, `LeaveTypeEndpoints.cs`, `LeaveTypeSeeder.cs`, `Features/GetLeaveTypes.cs`, and the whole `Modules/LeaveTypes/` folder. Remove the `LeaveTypeSeeder` call + `using Tsz.Api.Modules.LeaveTypes` + `LeaveTypeEndpoints.Map(app)` from `Program.cs`.
2. **API** — Reshape `UserLeave.cs`: add `Year` back, keep `TotalDays`, no `Allowed`. `UserLeaveConfiguration.cs` unique on `(UserId, LeaveTypeId, Year)`.
3. **API** — Reshape `UserLeaveDto.cs`: add `year`, `takenDays` (null), `balanceDays` (null), keep `defaultAllowed` (joined from LeaveType) and `leaveTypeName`. Drop `id`.
4. **API** — `Features/GetUserLeaves.cs`: take `year` (default current), return merged set per the materialization rule above. Joins `LeaveType` server-side. No DB writes.
5. **API** — `Features/UpsertUserLeave.cs` (rename from `UpdateUserLeave` or replace): upsert on `(userId, leaveTypeId, year)`. Validator + handler-level conditional. Returns the 3-way result.
6. **API** — `UserEndpoints.cs`: GET `/api/users/{userId}/leaves?year=` + PUT `/api/users/{userId}/leaves`. No POST/DELETE on leaves. No LeaveType endpoints.
7. **API** — `CreateUser.cs`: drop `TimeProvider` injection + the UserLeave seeding loop. User creation does not touch `UserLeave`.
8. **API** — Migration `LeavesModel` (replaces the pair shipped previously — `LeavesModel` + `SimplifyLeaves`). Bakes in `HasData()` inserts for the 4 LeaveType rows so prod gets them on `Migrate()`. If we already have one or both predecessors committed and applied to a dev DB, EF will treat the new desired state as the target — generate a single combined migration off the current model and commit it; resolve any duplicate migration filenames by deleting the predecessors. Dev DB resets clean via `EnsureCreated`.
9. **API** — Unit + integration tests per the matrix above. All passing.
10. **Regen** — start dev API, `bun --filter web gen:api` (use the `curl -k + bunx openapi-typescript` workaround if Node fetch chokes on the mkcert cert).
11. **Web** — API wrapper: `user-leaves.ts` (only `getUserLeaves` + `upsertUserLeave`). No `leave-types.ts`.
12. **Web** — `_protected/admin/users/$id.tsx`: restore the year prev/next nav, section heading is "Leave overview", drop any add/delete UI, edit dialog has a single Total field, Unlimited rows have no Edit button.
13. **Web** — Verify no `/admin/leave-types/` routes exist; no nav link for leave types; no magic strings (use `LeaveAllowed.Limited` etc.).
14. **Smoke test** the manual checklist.

## Follow-up plans (out of scope here)

- **`plan-leave-overview.md`** — user-facing `/leaves` (or `/leave-overview`) route: 12-month calendar grid + Balance panel per the mockup. This is what the user sees of their own leaves; tracks `requirements/leave-overview/leave-overview.md`. Materializes once the timesheet module is computing `takenDays` / `balanceDays`.
- **`plan-list.md`** — user list page enhancements (filter / active toggle / sort / total count / keyset pagination + infinite scroll). Already drafted at `docs/product/requirements/users/plan-list.md`.

## Edge cases & non-goals (recap)

- **`Taken` / `Balance` computation** — deferred to the timesheets module. DTO fields are nullable today; UI renders `—`; wiring the value later is one handler change.
- **`LeaveType` default drifting forward** — bumping `LeaveType.DefaultDays` retroactively changes any year that has no persisted `UserLeave` override. Acknowledged. Frozen-per-year defaults can land as `LeaveTypeYearOverride` in a later plan.
- **Recreated-in-Entra user** — unchanged from the existing user-provisioning plan.
- **Bulk operations / per-org leave types / role-driven leave allowances** — out of scope.
- **Calendar UI / day-cell rendering** — `plan-leave-overview.md`, future.
