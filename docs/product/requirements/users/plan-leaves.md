# Plan: Leaves — `LeaveType` (seeded) + per-user-per-year `UserLeave`

## Goal

Replace the four flat decimal columns on `User` (`HolidayDays / AdvDays / AncienniteitDays / SicknessDays`) with:

- A small fixed `LeaveType` reference list, seeded once at startup. Not admin-managed.
- A per-user × leave-type × year `UserLeave` row that stores **only** the admin-configured `TotalDays`.
- A `UserLeaveDto` that surfaces `year`, `totalDays`, `takenDays`, `balanceDays` to the frontend — `takenDays` and `balanceDays` are computed by the future timesheets module and stay `null` until then.

The admin uses this on the user edit page to set numbers per leave type per year. The numbers feed the leave-overview / timesheet flow when that module lands.

## Non-goals (explicit)

- **No admin CRUD on `LeaveType`.** The list is seeded and immutable from the UI. No `/admin/leave-types` page, no POST/PUT/DELETE endpoints. If the catalogue needs to grow, that's a separate plan (new seed, new migration if any).
- **No `Add leave` / `Delete leave` buttons** on the user edit page. The (user × leaveType, current year) rows are seeded by `CreateUser`. Admin can only edit values, not add/remove rows.
- **No calendar UI on the user detail page.** The leave-overview calendar mockup (12-month grid + Balance panel) is a separate plan, tracked under `requirements/leave-overview/leave-overview.md`. This plan only ships the *amounts* admin can set.
- **No `Allowed` per UserLeave.** Whether a leave type is `Limited` / `Unlimited` is a property of the `LeaveType` itself (`DefaultAllowed`). Admin can't override it per user.
- **No `Taken` / `Balance` computation.** Both stay `null` in the DTO. Wiring the contract now means zero churn when the timesheet module fills them.
- **No tenancy / per-org leave types.**
- **No year navigation in v1.** UI shows current year only. Past/future-year editing and year prev/next nav are deferred — they need an explicit "open year N" action that materializes rows, which is out of scope here. Future plan.
- **No automatic per-year roll-over job.** Same reason — deferred to the explicit-action plan.
- **No back-fill of existing users on catalogue growth.** When a new `LeaveType` is added later, existing users won't auto-get a row for it. Out of scope; future plan handles this with an explicit back-fill step.
- **No back-fill of existing users on this schema change.** Dev DB is wiped via `EnsureCreated`; no prod data.
- **No synthesized rows.** Every row in `UserLeaves` is a real persisted row. GET never invents rows on the fly. Reports can query the table directly with plain SQL.

## Data model

### `LeaveType` (new — `Modules/Users/`)

Immutable seeded reference data. No soft-delete. Lives in `Modules/Users/` as a **supporting entity** of the user-leaves slices — there is no `LeaveTypes` module and no standalone endpoint. The catalogue is implementation detail of the slices that join it into `UserLeaveDto`.

| Field            | Type           | Notes                                                                                |
| ---------------- | -------------- | ------------------------------------------------------------------------------------ |
| `Id`             | `Guid`         | PK.                                                                                  |
| `Name`           | `string`       | Required. Unique.                                                                    |
| `DefaultDays`    | `decimal?`     | Default total days. `null` for unlimited types. Copied into seeded `UserLeave` rows on user create. |
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
| `TotalDays`   | `decimal?` | Configured days for this user/year. Seeded from `LeaveType.DefaultDays` on user create; admin can later edit it. `null` when the type is Unlimited. |

Unique index on `(UserId, LeaveTypeId, Year)`. No soft-delete.

Rows are **eagerly created on user create** — one row per `LeaveType` for the current year. Every row is a real persisted row; there is no synthesis path. Past/future-year rows are out of scope for v1.

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

## Materialization — eager seeding on user create

`CreateUser` writes one `UserLeave` row per `LeaveType` for the **current year**. Every row carries `TotalDays = LeaveType.DefaultDays` at seed time. The admin can later edit those values via PUT.

```
CreateUser(name, email, role):
  user ← User.Create(...)
  leaveTypes ← all LeaveType rows
  year ← TimeProvider.GetUtcNow().Year
  for each leaveType:
    UserLeave.Create(user.Id, leaveType.Id, year, leaveType.DefaultDays)
  SaveChanges
```

`CreateUser` takes `TimeProvider` (already registered as `TimeProvider.System`).

### How GET works

```
GET /api/users/{userId}/leaves?year={year}
  1. SELECT UserLeaves WHERE UserId = userId AND Year = year
  2. JOIN LeaveType for name + defaultAllowed in the projection
  3. Return UserLeaveDto[]
```

No synthesis, no merge, no DB writes. If the year has no rows (any year other than the user-create year in v1), the response is `[]`. The v1 UI only shows the current year, so this case is invisible to users.

Reports query `UserLeaves` directly:

```sql
SELECT SUM(TotalDays) FROM UserLeaves WHERE Year = 2026   -- works
```

Catalogue drift trade-off: bumping `LeaveType.DefaultDays` from 20→22 does **not** retroactively change existing user rows — they hold the value that was current at seed time. New users created after the bump get 22. This is the opposite trade-off from the lazy-synthesis design and arguably more correct for HR data.

`UserLeaveDto` shape:

```jsonc
{
  "id": "...",                      // real PK; every row is persisted
  "leaveTypeId": "...",
  "leaveTypeName": "Verlof",
  "defaultAllowed": "Limited",      // from joined LeaveType — drives UI render
  "year": 2026,
  "totalDays": 20,                  // persisted value
  "takenDays": null,                // always null until timesheets ship
  "balanceDays": null               // always null until timesheets ship
}
```

## API surface

All endpoints require an authenticated Entra JWT. Admin gating per `AuthorizationPolicies.RequireAdmin` except where noted.

**No `LeaveType` endpoints.** No slice needs the catalogue as a standalone list — the user-leaves slices join `LeaveType` server-side and project name + `defaultAllowed` into `UserLeaveDto`. The future timesheets / leave-overview slices will do the same. If a future feature ever does need the standalone list (e.g. a filter dropdown), add a `GetLeaveTypes` slice then.

### UserLeaves

| Method | Path                                              | Auth                  | Body / Response                                                                                                                              |
| ------ | ------------------------------------------------- | --------------------- | -------------------------------------------------------------------------------------------------------------------------------------------- |
| GET    | `/api/users/{userId}/leaves?year={year}`          | JWT + `RequireAdmin`  | `200 UserLeaveDto[]` — persisted rows for that year, joined with LeaveType. `year` defaults to current year. May be empty for non-current years. |
| PUT    | `/api/users/{userId}/leaves`                      | JWT + `RequireAdmin`  | `UpdateUserLeavesBody { year, items: [{ id, totalDays? }] }` → `200 UserLeaveDto[]`. **Atomic bulk update**: all items succeed or none. Response is the full set of rows for that year. |

Only two endpoints. **No PUT-by-id** — bulk PUT is the only write path because the UI always saves the whole form at once. No POST (rows are created by `CreateUser` only, in v1). No DELETE (admin can't remove rows — they represent the user's allocation history). The "explicit-action" feature that ships year nav + back-fill will extend the API then.

#### Bulk PUT — body shape

```jsonc
// PUT /api/users/{userId}/leaves
{
  "year": 2026,
  "items": [
    { "id": "...", "totalDays": 22 },
    { "id": "...", "totalDays": 6  },
    { "id": "...", "totalDays": 0  }
  ]
}
```

- `year` is required and must match the `Year` on every referenced row (server-side guard).
- `items` is required and non-empty. Each `id` must reference an existing `UserLeave` row owned by `{userId}`.
- Server applies all updates in **one transaction**. Validation failure on any item → 400, **nothing committed**. DB errors → 500, **nothing committed**.

#### Bulk PUT — response shape

`200 UserLeaveDto[]` — all rows for `(userId, year)` after the update, joined with `LeaveType`. Not just the updated ones. This makes `formBase` re-baselining a trivial `saved = response` swap and keeps the client's view consistent if rows the request didn't touch were mutated by another admin in the meantime.

### Validation (FluentValidation)

The codebase pattern: validator does **all** checks (shape + DB lookups), `.WithError(SomeError)` attaches an `ErrorCodeBase` instance to each rule. The `ValidationBehavior<,>` pipeline runs the validator before the handler. Failures throw `FluentValidation.ValidationException`; `GlobalExceptionHandler` maps to HTTP by highest-severity `ErrorCategory`. Handlers assume input is valid and just return the DTO. No discriminated result types, no per-endpoint status mapping.

`UpdateUserLeavesValidator` (async, takes `IUnitOfWork`) validates the bulk command. Per-item rules use `RuleForEach(x => x.Items)` so failures are surfaced as `items[i].*` paths in `ProblemDetails.errors`, letting the frontend map them back to specific rows.

| Scope     | Rule                                                                                                | WithError                                  | Category    |
| --------- | --------------------------------------------------------------------------------------------------- | ------------------------------------------ | ----------- |
| Top-level | `UserId` not empty                                                                                  | `CommonErrors.Required`                    | Validation  |
| Top-level | `Year` in range (2000–2100)                                                                         | `CommonErrors.Invalid`                     | Validation  |
| Top-level | `Items` not empty                                                                                   | `CommonErrors.Required`                    | Validation  |
| Top-level | All `Items[i].Id` unique within the request                                                         | `CommonErrors.Invalid`                     | Validation  |
| Top-level | All referenced rows exist for `(item.Id, UserId, Year)` (single async DB lookup over the id set)    | `UserLeaveErrors.NotFound`                 | NotFound    |
| Per item  | `Items[i].Id` not empty                                                                             | `CommonErrors.Required`                    | Validation  |
| Per item  | `Items[i].TotalDays >= 0` when set                                                                  | `CommonErrors.Invalid`                     | Validation  |
| Per item  | Total/Allowed match — joined `LeaveType.DefaultAllowed`: Limited⇒non-null, otherwise null (async DB) | `UserLeaveErrors.TotalDaysAllowedMismatch` | Validation  |

The two async DB checks share **one query** that loads the referenced `UserLeave` rows + their joined `LeaveType.DefaultAllowed` up front; per-item rules read from that pre-loaded set. Avoids N+1 queries inside `RuleForEach`.

`UserLeaveErrors.cs` (new, in `Modules/Users/`):

```csharp
public sealed class UserLeaveErrors : ErrorCodeBase<UserLeaveErrors>
{
    private UserLeaveErrors(string code, string message, ErrorCategory category)
        : base(code, message, category) { }

    public static readonly UserLeaveErrors NotFound =
        new("ERR_USER_LEAVE_NOT_FOUND", "User leave not found.", ErrorCategory.NotFound);

    public static readonly UserLeaveErrors TotalDaysAllowedMismatch =
        new("ERR_USER_LEAVE_TOTAL_ALLOWED_MISMATCH",
            "TotalDays must be set for Limited leave types and null otherwise.",
            ErrorCategory.Validation);
}
```

User-existence is implicit via `(Id, UserId)` row-exists check — no extra `UserErrors.NotFound` needed. `LeaveType`-existence is implicit via the FK on the existing row.

### Handler & endpoint shape

```csharp
public sealed class UpdateUserLeavesHandler(IUnitOfWork uow)
    : ICommandHandler<UpdateUserLeavesCommand, IReadOnlyList<UserLeaveDto>>
{
    public async Task<IReadOnlyList<UserLeaveDto>> HandleAsync(UpdateUserLeavesCommand command, CancellationToken ct = default)
    {
        // Validator already confirmed: all rows exist for (Id, UserId, Year),
        // all total/allowed values consistent with each row's LeaveType.
        var repo = uow.RepositoryFor<UserLeave>();
        var ids = command.Items.Select(i => i.Id).ToHashSet();

        var rows = await repo.GetAllAsListAsync(
            ul => ul.UserId == command.UserId && ul.Year == command.Year && ids.Contains(ul.Id), ct);

        var byId = rows.ToDictionary(r => r.Id);
        foreach (var item in command.Items)
            byId[item.Id].SetTotalDays(item.TotalDays);

        await uow.SaveChangesAsync(ct);

        // Return the full set for the year so the form re-baselines from one source.
        var allRows = await repo.GetAllAsListAsync(
            ul => ul.UserId == command.UserId && ul.Year == command.Year, ct);
        var leaveTypes = (await uow.RepositoryFor<LeaveType>().GetAllAsListAsync(ct: ct))
            .ToDictionary(lt => lt.Id);

        return allRows
            .Select(r => UserLeaveDto.ToDto(r, leaveTypes[r.LeaveTypeId].Name, leaveTypes[r.LeaveTypeId].DefaultAllowed))
            .ToList();
    }
}
```

Command + body shape — `UserId` is populated from the route param; `Year` and `Items` come from the body:

```csharp
public sealed record UpdateUserLeavesCommand(Guid UserId, int Year, IReadOnlyList<UpdateUserLeavesItem> Items)
    : ICommand<IReadOnlyList<UserLeaveDto>>;

public sealed record UpdateUserLeavesItem(Guid Id, decimal? TotalDays);

public sealed record UpdateUserLeavesBody(int Year, IReadOnlyList<UpdateUserLeavesItem> Items);
```

Endpoint:

```csharp
adminGroup.MapPut("/{userId:guid}/leaves", async (
    Guid userId,
    UpdateUserLeavesBody body,
    IDispatcher dispatcher,
    CancellationToken ct) =>
{
    var command = new UpdateUserLeavesCommand(userId, body.Year, body.Items);
    var dtos = await dispatcher.SendAsync(command, ct);
    return Results.Ok(dtos);
});
```

HTTP mapping is automatic via `GlobalExceptionHandler`:
- Any item references a row that doesn't exist for `(Id, UserId, Year)` → 404 (`ERR_USER_LEAVE_NOT_FOUND`).
- Any item violates allowed/total → 400 (`ERR_USER_LEAVE_TOTAL_ALLOWED_MISMATCH`) keyed by `items[i].totalDays`.
- Shape failures (negative TotalDays, empty items, year out of range) → 400.
- All errors land in the same `ProblemDetails` payload (`code`, `errors[]`, etc.) the frontend already consumes. The `errors` dictionary keys (`items[0].totalDays`, etc.) let the form map errors back to specific rows.
- **Nothing partially commits.** Validation failure = nothing changed.

## CQRS slices (final file layout)

```
Modules/Users/
  LeaveType.cs                  ← supporting entity (no module of its own)
  LeaveAllowed.cs
  LeaveTypeConfiguration.cs     ← unique index on Name + HasData() seed for the 4 rows
  UserLeave.cs
  UserLeaveConfiguration.cs
  UserLeaveDto.cs               ← surface DTO; carries id + joined LeaveType fields
  UserErrors.cs                 ← existing; unchanged
  UserLeaveErrors.cs            ← new; NotFound + TotalDaysAllowedMismatch
  Features/
    GetUserLeaves.cs            ← plain query: persisted rows for the year, joined with LeaveType
    UpdateUserLeaves.cs         ← bulk atomic update for (userId, year); single transaction; returns full year set
  (existing) CreateUser.cs — KEEP TimeProvider injection + UserLeave seeding loop (current year × every LeaveType)
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

After API ships: `bun --filter web gen:api`. Schemas present: `LeaveAllowed`, `UserLeaveDto`, `UpdateUserLeaveBody`. No `LeaveTypeDto` (no endpoint exposes it).

### API wrappers (`packages/web/src/api/`)

- `user-leaves.ts` + `user-leaves.server.ts` — `getUserLeaves(userId, year)`, `updateUserLeave(userId, id, body)`. No add, no delete, no upsert.

No `leave-types.ts` wrapper — frontend never calls a LeaveType endpoint; everything it needs is on `UserLeaveDto`.

`LeaveAllowed` const + type + values array, modelled on `UserRole` (matches the existing pattern; no magic strings at call sites).

### Routes

- `src/routes/_protected/admin/users/$id.tsx` — **modify**: add a "Leave overview" section below the existing General section.

No `/admin/leave-types/` routes. No nav link for leave types.

### "Leave overview" section on the admin user edit page

Layout (admin-facing, single grid, **current year only, no year nav, no calendar, no Allowed column, always-visible inline inputs, batched Save/Cancel**):

```
┌ Leave overview — 2026 ─────────────────────────────────────────┐
│                                                                │
│ Name           Total           Taken  Balance                  │
│ Verlof         [  20   ]        —       —                      │
│ ADV dagen      [   5   ]        —       —                      │
│ Anciënniteit   [   0   ]        —       —                      │
│ Ziekte         Unlimited        —       —                      │
│                                                                │
│                                  [ Cancel ]  [ Save changes ]  │
└────────────────────────────────────────────────────────────────┘
```

- Section title is **"Leave overview — {year}"** where `{year}` is the current calendar year.
- **No year navigation in v1.** The grid renders the current year. Past/future years are out of scope until the explicit-action follow-up plan ships.
- One row per LeaveType for the current year — rows always exist because `CreateUser` seeded them.
- **No `Allowed` column.** `defaultAllowed` is consumed by the form internally to decide editability; it is not surfaced as a visible column.
- `Taken` / `Balance` cells render `—` (DTO returns `null` for now).
- `Unlimited` rows: Total cell renders a muted **"Unlimited"** label (no input, not editable). `Ziekte` is the only seeded Unlimited type today.
- `Limited` rows: Total cell **is** a `<Input type="number" min={0} step={0.5}>` (always-visible, no click-to-edit, no Edit button, no dialog).

#### Single form for the whole section — built on `useAppForm`

The entire "Leave overview" section is **one form** built with `useAppForm` from `#/components/form/form-context` (the project's TanStack-Form-based formBase). All bookkeeping — dirty tracking, disabled-button state, baseline-vs-current diffing, reset-to-baseline, server-error mapping — is handled by formBase primitives. Nothing about it is hand-rolled.

**Form shape**:

```ts
useAppForm({
  defaultValues: {
    items: leaves.map((l) => ({ id: l.id, leaveTypeId: l.leaveTypeId, totalDays: l.totalDays })),
  },
  validators: { onChange: leavesFormSchema }, // zod, mirrors server: totalDays >= 0 when set
  onSubmit: async ({ value }) => {
    clearServerErrors();
    try {
      const updated = await submitUpdateUserLeaves({
        data: { userId, body: { year: currentYear, items: value.items } },
      });
      form.reset({
        items: updated.map((l) => ({ id: l.id, leaveTypeId: l.leaveTypeId, totalDays: l.totalDays })),
      });
      await router.invalidate();
    } catch (e) {
      handleApiError(e);
    }
  },
});

const { serverError, clearServerErrors, handleApiError } = useFormServerErrors(form, [
  // formBase clears these server errors on next submit; the actual field paths are emitted
  // by the server (`items[i].totalDays`) and consumed by useFormServerErrors automatically.
  'items',
]);
```

- `defaultValues` is the array of editable items. All items go in — both Limited and Unlimited — so the payload always represents the full year set. Unlimited rows never change `totalDays` (no input), but they're still part of `value.items` so the form's idea of "saved state" stays consistent with what the server returns.
- "Dirty" is `!isEqual(values, defaultValues)`, deep-equality, handled by `FormActions` for you.
- Submit fires **one** `updateUserLeaves` call with the full items array. Server applies atomically; unchanged items are no-ops on the DB side.
- `form.reset(updated)` re-baselines from the response. No second GET.
- `useFormServerErrors(form, ['items'])` maps any `items[i].totalDays` errors from `ProblemDetails` onto the matching `form.AppField` meta so they render inline via the existing `<FieldError>` component.

**Render shape** (sketch):

```tsx
<form.AppForm>
  <form.FormErrorBanner message={serverError} />
  <form onSubmit={(e) => { e.preventDefault(); e.stopPropagation(); form.handleSubmit(); }}>
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>Name</TableHead>
          <TableHead>Total</TableHead>
          <TableHead>Taken</TableHead>
          <TableHead>Balance</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {leaves.map((l, i) => (
          <TableRow key={l.id}>
            <TableCell>{l.leaveTypeName}</TableCell>
            <TableCell>
              {l.defaultAllowed === LeaveAllowed.Unlimited ? (
                <span className="text-muted-foreground">Unlimited</span>
              ) : (
                <form.AppField name={`items[${i}].totalDays`}>
                  {(field) => <field.NumberField label="" min={0} />}
                </form.AppField>
              )}
            </TableCell>
            <TableCell>—</TableCell>
            <TableCell>—</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
    <div className="mt-4 flex justify-end">
      <form.FormActions cancel saveLabel="Save changes" />
    </div>
  </form>
</form.AppForm>
```

`<form.FormActions cancel />` already handles:
- Save disabled when `!isChanged` (deep-equal vs `defaultValues`), when `hasClientError`, or when `isSubmitting`.
- Cancel disabled when `!isChanged`. Click → `form.reset()` reverts to `defaultValues` (= last known server state, kept current by the post-submit `form.reset(updated)`).
- Pending label swap on the Save button while in flight.

No per-cell "dirty ring" is required — TanStack Form's field meta exposes per-field dirty state if Pieter wants to add a visual cue later, but the standard `<FormActions>` + per-field validation styling is enough for v1. **No spec for a dirty ring; defer to whatever the existing forms already do.**

The form is invalid (disables Save) any time a Limited row's `totalDays` is < 0 or non-numeric — zod handles this via `validators.onChange`.

### Nav

No new nav entries. The Users link admins already see is enough — they access the Leave overview by drilling into a user.

## Tests

### Unit (`Tsz.Api.Tests`)

- `Builders/LeaveTypeBuilder.cs` (simple — `WithName`, `Limited(days)`, `Unlimited()`).
- `Builders/UserLeaveBuilder.cs` (`ForUser`, `ForType`, `Year`, `WithTotal`).
- `GetUserLeavesHandlerTests`:
  - User with seeded current-year rows → returns one row per `LeaveType` joined with name + `defaultAllowed`.
  - GET for a year with no rows → returns `[]`.
  - DTO carries `id`, `leaveTypeName`, `defaultAllowed`, `totalDays`.
- `UpdateUserLeavesHandlerTests` (validator mocked / passing — handler-only behaviour):
  - Bulk update of multiple rows → all rows mutated in one transaction, response returns the **full year set** (not just the changed ones).
  - Response DTOs carry the joined `leaveTypeName` + `defaultAllowed`.
  - Subset update (only some items changed) → untouched rows preserved unchanged in the response.
- `UpdateUserLeavesValidatorTests`:
  - Top-level shape: `UserId` empty → `CommonErrors.Required`.
  - Top-level shape: `Year` out of 2000–2100 → `CommonErrors.Invalid`.
  - Top-level shape: `Items` empty → `CommonErrors.Required`.
  - Top-level shape: duplicate `Items[i].Id` in the request → `CommonErrors.Invalid`.
  - Per-item shape: `Items[i].Id` empty → `CommonErrors.Required` keyed by `items[i].id`.
  - Per-item shape: `Items[i].TotalDays < 0` → `CommonErrors.Invalid` keyed by `items[i].totalDays`.
  - DB: any `Items[i].Id` references a row not owned by `(UserId, Year)` → `UserLeaveErrors.NotFound` keyed by `items[i].id`.
  - DB: row's LeaveType is `Limited` with `TotalDays == null` → `UserLeaveErrors.TotalDaysAllowedMismatch` keyed by `items[i].totalDays`.
  - DB: row's LeaveType is `Unlimited` with `TotalDays != null` → `UserLeaveErrors.TotalDaysAllowedMismatch` keyed by `items[i].totalDays`.
  - Happy path: Limited + non-null total, Unlimited + null total → no failures.
  - Atomicity: when one item fails, the validator surfaces failures for all bad items in one response (no early return).
- `CreateUserHandlerTests` — assert that user-create writes one `UserLeave` row per `LeaveType` with `Year == TimeProvider.GetUtcNow().Year` and `TotalDays == LeaveType.DefaultDays`.

### Integration (`Tsz.Api.Tests.Integration`)

- `UserLeaveEndpointsTests` (HTTP status is the assertion — `GlobalExceptionHandler` does the mapping from error codes):
  - GET for a freshly created user returns the seeded current-year set (count == seeded LeaveType count).
  - GET for a year with no rows returns `[]`.
  - Bulk PUT with valid items → 200, response is the full year set with updated values, subsequent GET reflects the changes.
  - Bulk PUT containing one invalid item (negative total) → 400 with `code: ERR_INVALID` keyed under `items[i].totalDays`. **No rows changed in the DB.**
  - Bulk PUT referencing a row that doesn't exist for `(UserId, Year)` → 404 with `code: ERR_USER_LEAVE_NOT_FOUND` keyed under `items[i].id`. **No rows changed.**
  - Bulk PUT with an Unlimited-type item carrying non-null total → 400 with `code: ERR_USER_LEAVE_TOTAL_ALLOWED_MISMATCH` keyed under `items[i].totalDays`.
  - Bulk PUT with a Limited-type item carrying `null` total → 400 same error.
  - Bulk PUT with mismatched `body.Year` vs the row's actual `Year` → 404 (row not found for that `(Id, UserId, Year)`).
  - Bulk PUT with empty `items` → 400 with `code: ERR_REQUIRED`.
  - Bulk PUT with duplicate `items[i].Id` → 400 with `code: ERR_INVALID`.
  - Non-admin → 403.
  - Soft-deleted user → 404 (no leak through query filter).

## Smoke test plan (manual, after implementation)

1. Sign in as seeded admin Pieter.
2. Open `/admin/users/$id` for Pieter → "Leave overview — 2026" section appears below General.
3. Grid shows 4 rows seeded by `CreateUser`: Verlof (20), ADV dagen (5), Anciënniteit (0), Ziekte (Total cell shows muted "Unlimited" label, no input). No Allowed column. Save / Cancel buttons are disabled.
4. Change Verlof from `20` → `22`. Save and Cancel buttons enable (form is dirty). No request fired yet (verify in network tab).
5. Also change ADV dagen from `5` → `6`. Both buttons still enabled.
6. Click **Cancel** → both inputs revert (20 and 5). Buttons disable. No requests fired.
7. Change Verlof to `22` and ADV to `6` again, then click **Save changes** → button shows pending label, **one** `PUT /api/users/{id}/leaves` fires with `{ year, items: <full set including Unlimited rows> }`, response is the full year set, form re-baselines via `form.reset(...)`, buttons disable.
8. Verify DB: Verlof row has `TotalDays = 22`, ADV has `6`. Anciënniteit + Ziekte unchanged.
9. Change Verlof to `-1` → zod's `onChange` validator flags the field; the `<FieldError>` shows inline; Save button stays disabled. No request fires.
10. To exercise the server-side error path: open devtools, bypass the client zod by temporarily editing the value back to a valid number, change again in flight, etc. Realistically the server-side 400 path is only hit for invariants client zod can't check (e.g. an Unlimited row's `totalDays` being set non-null — not reachable from the UI). Confirm via API client (curl/scalar) that PUT with `items[0].totalDays = -1` returns 400 with `items[0].totalDays: ERR_INVALID` and that **the DB is unchanged for every item** (atomic rollback).
11. Ziekte's Total renders the muted "Unlimited" label with no input — cannot be edited.
12. Sign in as a non-admin → `/admin/users/$id` redirects to `/` (existing gate). API `PUT /api/users/.../leaves` returns 403.
13. Create a new user via admin UI → open their Leave overview → grid shows 4 rows immediately with default values. Confirm via DB that **4** `UserLeave` rows exist for the new user (one per LeaveType, current year, `TotalDays` = each LeaveType's `DefaultDays`).

## Steps

1. **API** — Collapse the LeaveType files into `Modules/Users/`: keep `LeaveType.cs` (no `DeletedAt`), `LeaveAllowed.cs`, `LeaveTypeConfiguration.cs` (plain unique index on Name + `HasData()` for the 4 catalogue rows with deterministic GUIDs). Delete `LeaveTypeDto.cs`, `LeaveTypeEndpoints.cs`, `LeaveTypeSeeder.cs`, `Features/GetLeaveTypes.cs`, and the whole `Modules/LeaveTypes/` folder. Remove the `LeaveTypeSeeder` call + `using Tsz.Api.Modules.LeaveTypes` + `LeaveTypeEndpoints.Map(app)` from `Program.cs`.
2. **API** — Reshape `UserLeave.cs`: add `Year`, keep `TotalDays`, no `Allowed`. Factory `UserLeave.Create(userId, leaveTypeId, year, totalDays)`. `UserLeaveConfiguration.cs` unique on `(UserId, LeaveTypeId, Year)`.
3. **API** — Reshape `UserLeaveDto.cs`: keep `id`, add `year`, `takenDays` (null), `balanceDays` (null), keep `defaultAllowed` (joined from LeaveType) and `leaveTypeName`.
4. **API** — `Features/GetUserLeaves.cs`: take `year` (default current), return persisted rows joined with `LeaveType`. No DB writes, no synthesis, no merge. May return `[]`.
5. **API** — Create `UserLeaveErrors.cs` (`NotFound`, `TotalDaysAllowedMismatch`). `Features/UpdateUserLeaves.cs`: command `UpdateUserLeavesCommand(UserId, Year, IReadOnlyList<UpdateUserLeavesItem>)`. Validator does all checks via `.WithError(...)` per the rule matrix above — top-level shape + per-item shape with `RuleForEach(x => x.Items)` so failures key under `items[i].*` paths. The two async DB checks share **one query** loading all referenced rows + joined `LeaveType` up front, then per-item rules read from that pre-loaded set (avoid N+1 inside `RuleForEach`). Handler is clean — loads rows by id-set, mutates each, single `SaveChangesAsync` (one transaction), returns the **full year set** as `IReadOnlyList<UserLeaveDto>`. No upsert, no per-row endpoint.
6. **API** — `UserEndpoints.cs`: GET `/api/users/{userId}/leaves?year=` + PUT `/api/users/{userId}/leaves` (bulk). No POST/DELETE on leaves. No LeaveType endpoints. No PUT-by-id. PUT dispatches + `Results.Ok(dtos)` — error mapping is automatic via `GlobalExceptionHandler`. Drop the existing `POST / PUT-by-id / DELETE` leave endpoints.
7. **API** — `CreateUser.cs`: KEEP `TimeProvider` injection + UserLeave seeding loop. On create, write one `UserLeave` per `LeaveType` for `time.GetUtcNow().Year` with `TotalDays = leaveType.DefaultDays`.
8. **API** — Migration `LeavesModel` (replaces the pair shipped previously — `LeavesModel` + `SimplifyLeaves`). Bakes in `HasData()` inserts for the 4 LeaveType rows so prod gets them on `Migrate()`. If we already have one or both predecessors committed and applied to a dev DB, EF will treat the new desired state as the target — generate a single combined migration off the current model and commit it; resolve any duplicate migration filenames by deleting the predecessors. Dev DB resets clean via `EnsureCreated`.
9. **API** — Unit + integration tests per the matrix above. All passing.
10. **Regen** — start dev API, `bun --filter web gen:api` (use the `curl -k + bunx openapi-typescript` workaround if Node fetch chokes on the mkcert cert).
11. **Web** — API wrapper: `user-leaves.ts` (only `getUserLeaves(userId, year)` + `updateUserLeaves(userId, body)` — the bulk variant). No `leave-types.ts`. No per-row `updateUserLeave`.
12. **Web** — `_protected/admin/users/$id.tsx`: replace the existing `LeavesSection` + `EditLeaveDialog` entirely. Section heading is "Leave overview — {currentYear}", **no** year nav, **no Allowed column**, no add/delete UI, no Edit button, no dialog. Build the whole section as one `useAppForm` (from `#/components/form/form-context`) with `defaultValues: { items: leaves.map(l => ({ id, leaveTypeId, totalDays })) }` (full set — Limited and Unlimited) and `validators: { onChange: leavesFormSchema }` (zod: `totalDays >= 0` when set). Render rows in a `<Table>`; for Unlimited rows render a muted "Unlimited" span in the Total cell, for Limited rows render `<form.AppField name={\`items[${i}].totalDays\`}>{(field) => <field.NumberField label="" min={0} />}</form.AppField>`. Below the grid: right-aligned `<form.FormActions cancel saveLabel="Save changes" />`. `onSubmit` calls `submitUpdateUserLeaves({ data: { userId, body: { year: currentYear, items: value.items } } })` (a `createServerFn` wrapping `updateUserLeaves`), then `form.reset({ items: response.map(...) })` and `router.invalidate()`. Wire `useFormServerErrors(form, ['items'])` for inline per-row error surfacing via `<FieldError>`. `<form.FormErrorBanner message={serverError} />` above the form for top-level errors. **Do not hand-roll dirty tracking, button disabled state, or cancel/reset — `<form.FormActions>` handles all of it.**
13. **Web** — Verify no `/admin/leave-types/` routes exist; no nav link for leave types; no magic strings (use `LeaveAllowed.Limited` etc.).
14. **Smoke test** the manual checklist.

## Follow-up plans (out of scope here)

- **`plan-leave-overview.md`** — user-facing `/leaves` (or `/leave-overview`) route: 12-month calendar grid + Balance panel per the mockup. This is what the user sees of their own leaves; tracks `requirements/leave-overview/leave-overview.md`. Materializes once the timesheet module is computing `takenDays` / `balanceDays`.
- **`plan-list.md`** — user list page enhancements (filter / active toggle / sort / total count / keyset pagination + infinite scroll). Already drafted at `docs/product/requirements/users/plan-list.md`.

## Edge cases & non-goals (recap)

- **`Taken` / `Balance` computation** — deferred to the timesheets module. DTO fields are nullable today; UI renders `—`; wiring the value later is one handler change.
- **`LeaveType` default change** — bumping `LeaveType.DefaultDays` does **not** affect existing `UserLeave` rows (the value was snapshotted at seed time). New users created afterward get the new default. This is the opposite trade-off from lazy synthesis and arguably better for HR data.
- **Year navigation / past-year editing / future-year rows** — out of scope; future plan with an explicit "open year" action.
- **Back-fill of existing users when catalogue grows** — out of scope; future plan with an explicit migration/admin action.
- **Recreated-in-Entra user** — unchanged from the existing user-provisioning plan.
- **Bulk operations / per-org leave types / role-driven leave allowances** — out of scope.
- **Calendar UI / day-cell rendering** — `plan-leave-overview.md`, future.
