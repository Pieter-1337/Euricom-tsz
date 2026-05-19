# Plan: Multi-Role Support for Users

## Goal

Replace `User.Role` (single enum) with `User.Roles` (collection) so one user can hold multiple roles simultaneously (e.g., Admin + ClientManager).

## Context

Current state from codebase research:

- **Backend (`packages/api/Tsz.Api`):**
  - `Modules/Users/User.cs` — `UserRole Role` (private setter, not nullable), mutated via `ChangeRole`.
  - `UserRole` enum: `User = 0`, `Admin = 1`, `ClientManager = 2`.
  - `UserConfiguration.cs` — stores role as TEXT via `HasConversion<string>()`.
  - `RequireAdminAuthorizationHandler.cs` — checks `user.Role == UserRole.Admin` directly.
  - Single policy `AuthorizationPolicies.RequireAdmin` enforced on `/api/users/*` (except `/me`).
  - `CreateUser.cs` / `UpdateUser.cs` accept a single `UserRole Role`.
  - `UserSeeder.cs` seeds one admin (`pieter.bracke@euri.com`).
- **Frontend (`packages/web`):**
  - Generated schema exposes `role: UserRole`.
  - `src/api/users.ts` has `USER_ROLES` constants for selects.
  - `user-create-form.tsx` / `user-edit-card.tsx` use a single `SelectField`.
  - No frontend role gating — backend enforces 401/403.
- **DB:** flat `Role` column on Users; no junction table; one existing seeded admin row to backfill.

Design decisions:

- **Storage:** owned value object `UserRoleAssignment` via EF `OwnsMany` → separate `UserRoles` table with composite PK `(UserId, Role)`. Queryable and indexable.
- **Frontend control:** Combobox with multiselect (shadcn `Popover` + `Command` with checkmarks per option, selected roles rendered as removable badges in the trigger).
- **Min 1 role required.** Duplicates rejected by validator.
- **No backwards compat:** drop `Role` column; migration backfills existing rows.

## Files

### Backend (`packages/api/Tsz.Api`)

- `Modules/Users/User.cs` — modify: replace `Role` property/`ChangeRole` with `Roles` collection + `AddRole` / `RemoveRole` / `SetRoles` mutators; update `Create` factory signature.
- `Modules/Users/UserRoleAssignment.cs` — create: owned value object `{ UserRole Role }`.
- `Modules/Users/UserConfiguration.cs` — modify: remove `Role` mapping; add `OwnsMany<UserRoleAssignment>` mapped to table `UserRoles` with composite PK `(UserId, Role)`, `Role` stored as string.
- `Modules/Users/Features/CreateUser.cs` — modify: command `Roles` (`IReadOnlyCollection<UserRole>`); validator requires non-empty, distinct, defined enum values; handler calls `User.Create(..., roles)`.
- `Modules/Users/Features/UpdateUser.cs` — modify: command `Roles`; validator same; handler calls `user.SetRoles(command.Roles)`.
- `Modules/Users/Features/GetUserById.cs`, `GetUsersPaged.cs`, `GetCurrentUser.cs` — modify: include `Roles` in responses; ensure `.Include`/projection loads the owned collection.
- `Modules/Users/UserDto.cs` (and feature-local response records if applicable) — modify: `Roles: IReadOnlyCollection<UserRole>` replaces `Role`.
- `Modules/Users/Authorization/RequireAdminAuthorizationHandler.cs` — modify: succeed when `user.Roles.Any(r => r == UserRole.Admin)`.
- `Modules/Users/UserSeeder.cs` — modify: seed admin with `roles: [UserRole.Admin]`.
- `Persistence/Migrations/<timestamp>_UserRoles.cs` (+ snapshot) — create: new EF migration; hand-edit `Up()` to add backfill SQL between `CreateTable("UserRoles")` and `DropColumn("Role", "Users")`; `Down()` reverses.

### Backend tests

- `Tsz.Api.Tests/Builders/UserBuilder.cs` — modify: replace `WithRole` with `WithRoles(params UserRole[])`; default `[UserRole.User]`.
- `Tsz.Api.Tests/Modules/Users/CreateUserHandlerTests.cs` — modify/add: multi-role create, validator rejects empty `Roles`, rejects duplicates, rejects undefined enum values.
- `Tsz.Api.Tests/Modules/Users/UpdateUserHandlerTests.cs` — modify/add: `SetRoles` replaces full set.
- `Tsz.Api.Tests/Modules/Users/UserTests.cs` (if absent, create) — `AddRole` is idempotent; `SetRoles` rejects empty; entity invariants.
- `Tsz.Api.Tests.Integration/UserEndpointsTests.cs` — modify/add: POST create with `[Admin, ClientManager]`; GET returns both; PUT update replaces; admin policy passes when Admin is among many; PUT removing Admin → subsequent admin call returns 403.

### Frontend (`packages/web`)

- `src/api/schema.ts` — regenerate via `bun --filter web gen:api`.
- `src/api/users.ts` — modify: keep `UserRole` + `USER_ROLES`; remove single-role helpers if any.
- `src/features/users/schemas.ts` — modify: `roles: z.array(z.enum(USER_ROLES)).min(1)`; drop single-role validation.
- `src/components/form/multi-select-combobox.tsx` (or in `src/components/ui/`) — create: shadcn `Popover` + `Command` based combobox supporting multiple selection; trigger shows selected items as removable `Badge` chips; check icon next to selected options; "Clear" action.
- `src/components/form/form-context.tsx` — modify if needed: bind the new multi-select combobox as `MultiSelectField` for `useAppForm` (mirror existing `SelectField` wiring).
- `src/features/users/components/user-create-form.tsx` — modify: replace role `SelectField` with `MultiSelectField`; default `['User']`.
- `src/features/users/components/user-edit-card.tsx` — modify: same swap; initial value from `user.roles`.
- `src/features/users/components/*` list/detail — modify: where `role` rendered, map `roles` → array of `Badge`.
- `src/features/users/server-fns.ts` — modify: submit `roles` array on create/update.

## Steps

1. **User entity** — add `private readonly List<UserRoleAssignment> _roles = new();` + public `IReadOnlyCollection<UserRole> Roles => _roles.Select(r => r.Role).ToList();`. Add `SetRoles(IEnumerable<UserRole>)` (rejects empty, dedupes), `AddRole`, `RemoveRole`. Update `Create` to `Create(..., IEnumerable<UserRole> roles)`. Remove `Role` and `ChangeRole`.
2. **Owned value object** — `UserRoleAssignment { UserRole Role }` in `Modules/Users/UserRoleAssignment.cs`.
3. **EF config** — in `UserConfiguration.cs`, remove `Property(u => u.Role)`; add:
   ```csharp
   builder.OwnsMany(u => u.RoleAssignments, ra => {
       ra.ToTable("UserRoles");
       ra.WithOwner().HasForeignKey("UserId");
       ra.Property(r => r.Role).HasConversion<string>().HasMaxLength(32);
       ra.HasKey("UserId", nameof(UserRoleAssignment.Role));
   });
   ```
   (Expose `RoleAssignments` as `internal` collection on `User` for EF; `Roles` stays the read API.)
4. **Migration** — run via the `ef-migration` skill (`dotnet ef migrations add UserRoles`). Then hand-edit `Up()`:
   - `migrationBuilder.CreateTable("UserRoles", ...)` (generated)
   - **Insert backfill SQL** (raw): `INSERT INTO UserRoles (UserId, Role) SELECT Id, Role FROM Users WHERE Role IS NOT NULL;`
   - `migrationBuilder.DropColumn(name: "Role", table: "Users");` (generated)
   - `Down()`: re-add `Role` column, copy first role back per user, drop `UserRoles`.
5. **Auth handler** — `RequireAdminAuthorizationHandler.HandleRequirementAsync`: succeed when `currentUser.Roles.Contains(UserRole.Admin)`.
6. **Commands & validators** — `CreateUserCommand.Roles: IReadOnlyCollection<UserRole>`; validator: `.NotEmpty()`, `.Must(r => r.Distinct().Count() == r.Count)`, `.ForEach(x => x.IsInEnum())`. Same for `UpdateUserCommand`. Handlers pass `command.Roles` through.
7. **DTOs / responses** — replace `Role` with `Roles` in user response records (`GetUserById`, `GetUsersPaged.Item`, `GetCurrentUser`, `CreateUser`/`UpdateUser` responses). Ensure queries `.Include` or project the owned collection.
8. **Seeder** — `User.Create(..., roles: new[] { UserRole.Admin })`.
9. **Backend unit tests** — handler tests + entity tests per file list above. Use the updated `UserBuilder`.
10. **Backend integration tests** — use the `backend-integration-test` skill to extend `UserEndpointsTests` per file list above.
11. **Regenerate frontend schema** — `bun --filter web gen:api`.
12. **Multi-select combobox component** — create `src/components/form/multi-select-combobox.tsx`:
    - Props: `options: { label, value }[]`, `value: string[]`, `onChange: (next: string[]) => void`, `placeholder`, `emptyText`.
    - shadcn `Popover` with a `Button` trigger; trigger renders selected values as `Badge` chips with an `X` icon to remove individually; placeholder when empty.
    - Inside: `Command` with `CommandInput` (filter), `CommandList`, `CommandGroup`, `CommandItem` per option toggling selection; `Check` icon next to selected items.
    - `CommandSeparator` + `CommandItem` "Clear" at the bottom when any selected.
    - Closes via popover open state; multi-select keeps it open after each pick.
13. **Form binding** — add `MultiSelectField` to the shared `useAppForm` field set in `form-context.tsx` (or a small wrapper file) so it integrates with field errors like `SelectField`.
14. **Update create form** — `user-create-form.tsx`: replace role `SelectField` with `MultiSelectField`; options from `USER_ROLES`; default `['User']`; submit `roles` array.
15. **Update edit form** — `user-edit-card.tsx`: same swap; initial value from `user.roles`.
16. **Update displays** — anywhere `role` is rendered (list rows, detail card), map `user.roles` → `<Badge>` per role.
17. **Manual verify** — start dev (`bun --filter web dev` + API), create a user with two roles, confirm DB row, hit an admin endpoint as that user, remove Admin, re-hit endpoint expects 403.

## Tests

- **Unit:**
  - `CreateUserHandler` accepts and persists multiple roles; validator rejects empty `Roles`; validator rejects duplicates; validator rejects undefined enum values.
  - `UpdateUserHandler.SetRoles` replaces full set (no leftover prior roles).
  - `User.AddRole` idempotent (adding existing role is a no-op); `SetRoles` rejects empty.
- **Integration:**
  - POST `/api/users` with `roles: ["Admin", "ClientManager"]` → 201, GET returns both.
  - PUT `/api/users/{id}` updates `roles` to `["User"]` → subsequent admin-only call returns 403.
  - Admin-only endpoint accessible to a user whose `roles` contains `Admin` among others → 200.
  - Validator surface: empty `roles` → 400; duplicate `roles` → 400.

## Edge Cases

- **Empty roles** — rejected at validator and entity level; entity throws if `SetRoles` receives empty.
- **Duplicate roles** — validator rejects (surface client bugs rather than silently deduping).
- **Self-demotion** — admin can remove their own Admin role; no guard (flag if you want one).
- **Backfill** — single existing seeded admin row migrates cleanly via raw SQL in `Up()`.
- **Owned collection loading** — confirm list/detail queries load `RoleAssignments` (EF eagerly loads owned collections by default, but verify in integration tests).
- **Better Auth session** — already does not carry role; unaffected.

## Assumptions

- Junction table via EF owned value object, **not** a JSON primitive collection.
- `Users.Role` column is dropped, **not** kept as a "primary role".
- Min 1 role enforced; no roleless users.
- Frontend control = combobox with multiselect (shadcn `Popover` + `Command`, removable badges in trigger).
- No frontend route gating in this change — backend remains the authority.
- Better Auth integration untouched.
