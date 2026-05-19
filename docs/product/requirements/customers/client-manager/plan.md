# Plan: Assign ClientManager to Customer

## Goal

Let an admin assign one of the users with the `ClientManager` role to a
customer, and prevent that user's `ClientManager` role from being removed
(or the user from being soft-deleted) while any customer is still linked
to them.

## Context

Codebase state after the multi-role commit (7873e25):

- **User** (`packages/api/Tsz.Api/Modules/Users/User.cs`) holds
  `Roles: IReadOnlyCollection<UserRole>` backed by owned
  `UserRoleAssignment` rows in the `UserRoles` table. `SetRoles` replaces
  the whole set; `RemoveRole` removes one.
- **Customer** (`packages/api/Tsz.Api/Modules/Customers/Customer.cs`) is a
  clean DDD entity (private setters, named mutators, soft delete via
  global query filter). Owned `Address` + `ContactPerson` value objects.
- **Cross-module convention**: modules reference each other by `Guid`
  FK only, never by navigation property. The `Customer` module therefore
  carries `Guid? ClientManagerId`, not a `User` reference.
- **Validation pattern**: `UpdateCustomerValidator` already does
  `MustAsync(...).WithError(...)` for cross-aggregate existence checks
  through `IUnitOfWork`. Same pattern fits the new checks.
- **Plain user list** (`GET /api/users`, `GetUsersHandler`) returns
  `IReadOnlyList<UserDto>` and is reserved for dropdown/lookup
  consumers. Extending it with an optional `?role=` filter is the
  cheapest way to feed the customer-form combobox.
- **Frontend customer form** (`packages/web/src/features/customers/
  components/customer-create-form.tsx` and `customer-edit-card.tsx`)
  uses `useAppForm` + bound field components; `ComboboxField` is
  already wired for the country picker, so the same field type fits
  the client-manager picker.

Design decisions:

- **Optional link.** `Customer.ClientManagerId` is nullable. Existing
  customers and new ones aren't required to have one.
- **Role check on assignment.** Customer create/update validator rejects
  a `ClientManagerId` whose user doesn't currently have the
  `ClientManager` role.
- **Block role removal while linked.** `UpdateUserValidator` queries the
  customers repo (with default query filter, so soft-deleted customers
  don't count); if the user currently has `ClientManager`, the new roles
  don't include it, and any non-deleted customer still references them,
  the update is rejected with a Conflict.
- **Block soft-delete while linked.** Same check on `DeleteUserValidator`:
  same error code applies.
- **No nav property on Customer.** Cross-module decoupling matters more
  than EF-side join ergonomics; lookups happen through the user repo.
- **No FK constraint at DB level.** Same reason — modules own their
  tables. The validator enforces referential integrity.

## Files

### Backend (`packages/api/Tsz.Api`)

- `Modules/Customers/Customer.cs` — modify: add `Guid? ClientManagerId
  { get; private set; }`; add `AssignClientManager(Guid? userId)`
  mutator; extend `Create` factory with `clientManagerId` (optional).
- `Modules/Customers/CustomerConfiguration.cs` — modify: map
  `ClientManagerId` as a regular nullable `Guid` column (no FK
  constraint, no nav property).
- `Modules/Customers/CustomerDto.cs` — modify: add
  `ClientManagerId: Guid?` to `CustomerDto`, projection, and
  `ToDto`.
- `Modules/Customers/Features/CreateCustomer.cs` — modify: command
  gains `ClientManagerId: Guid?`; validator: when not null, must
  reference an existing non-deleted user **and** that user must have
  `ClientManager` role; handler passes id to `Customer.Create`.
- `Modules/Customers/Features/UpdateCustomer.cs` — modify: same
  command/validator additions; handler calls
  `customer.AssignClientManager(command.ClientManagerId)`.
- `Modules/Customers/CustomerErrors.cs` — modify: add
  `ClientManagerNotFound` (NotFound) and
  `ClientManagerMissingRole` (Validation).
- `Modules/Users/UserErrors.cs` — modify: add
  `CannotRemoveClientManagerRoleWhileAssigned` (Conflict) — message:
  "User is still assigned as ClientManager to one or more customers."
- `Modules/Users/Features/UpdateUser.cs` — modify: validator gains a
  conditional `MustAsync` rule. Triggers when (a) the user being
  updated currently has `ClientManager` and (b) the incoming `Roles`
  doesn't contain `ClientManager`. Fails if any non-deleted customer
  has `ClientManagerId == userId`.
- `Modules/Users/Features/DeleteUser.cs` — modify: validator gains the
  same "no customer assignments" guard, gated on the user currently
  having `ClientManager`.
- `Modules/Users/Features/GetUsers.cs` — modify: query gains optional
  `Role: UserRole?` filter; handler applies
  `u => u.RoleAssignments.Any(r => r.Role == role)` when set. Endpoint
  binds `?role=` from query string.
- `Modules/Users/UserEndpoints.cs` — modify: `GET /api/users` accepts
  `?role=` (parsed via `Enum.TryParse`); pass into the query.
- `Persistence/Migrations/<timestamp>_CustomerClientManager.cs` —
  create: add `ClientManagerId` column (nullable Guid) to `Customers`.
  Generated via `ef-migration` skill. No backfill — column defaults
  to NULL.

### Backend tests

- `Tsz.Api.Tests/Builders/CustomerBuilder.cs` — modify (or create if
  missing): add `WithClientManager(Guid? id)`.
- `Tsz.Api.Tests/Modules/Customers/Features/CreateCustomerHandlerTests.cs`
  — modify: happy path with valid ClientManager; happy path with null;
  validator rejects missing user; validator rejects user without
  `ClientManager` role.
- `Tsz.Api.Tests/Modules/Customers/Features/UpdateCustomerHandlerTests.cs`
  — modify: assign on update; clear by setting to null; same validator
  failures as create.
- `Tsz.Api.Tests/Modules/Users/Features/UpdateUserHandlerTests.cs` —
  modify: validator rejects removing `ClientManager` while a customer
  is assigned; allows removing when no customer is linked; allows
  removing when only soft-deleted customers are linked.
- `Tsz.Api.Tests/Modules/Users/Features/DeleteUserHandlerTests.cs` —
  modify: same three cases for soft-delete.
- `Tsz.Api.Tests/Modules/Users/Features/GetUsersHandlerTests.cs` —
  modify: returns all users when no role filter; filters by role when
  provided; returns empty when no user matches.
- `Tsz.Api.Tests.Integration/CustomerEndpointsTests.cs` — modify: POST
  with `ClientManagerId` → 201, GET reflects it; POST with id of a
  non-ClientManager → 400; PUT to assign / clear; PUT with invalid id
  → 400.
- `Tsz.Api.Tests.Integration/UserEndpointsTests.cs` — modify: PUT
  removing `ClientManager` while assigned → 409 with
  `ERR_USER_CANNOT_REMOVE_CLIENT_MANAGER_ROLE_WHILE_ASSIGNED`; PUT
  removing role after unassigning → 200; DELETE same two cases; GET
  with `?role=ClientManager` returns only client managers.

### Frontend (`packages/web`)

- `src/api/schema.ts` — regenerate via `bun --filter web gen:api`
  (picks up `ClientManagerId` on customer DTOs/commands and the
  `?role` param on GET users).
- `src/api/users.server.ts` — modify: `getUsers` accepts an optional
  `{ role?: UserRole }`; pass through.
- `src/features/users/server-fns.ts` — modify: add `fetchUsersByRole`
  createServerFn (or extend existing) so the customer form can call it
  via TanStack Query.
- `src/features/customers/schemas.ts` — modify: `customerFormSchema`
  gains `clientManagerId: z.string().uuid().nullable()` (with
  empty-string-to-null coercion).
- `src/features/customers/server-fns.ts` — modify: `toCreateRequest` /
  `toUpdateRequest` map `clientManagerId` (empty → null).
- `src/features/customers/components/customer-create-form.tsx` —
  modify: add `ClientManagerField` (a `ComboboxField` whose options
  are loaded from `fetchUsersByRole('ClientManager')` via
  `useQuery`); default `null`.
- `src/features/customers/components/customer-edit-card.tsx` — modify:
  same field; initial value from `customer.clientManagerId`.
- `src/features/customers/components/customers-list.tsx` — optional:
  add a "Client manager" column rendering the user's name (requires
  joining users; cheaper to defer and skip in v1).

## Steps

1. **Backend — Customer entity & config**
   - Add `Guid? ClientManagerId` (private setter) on `Customer`.
   - Add `AssignClientManager(Guid? userId)` mutator.
   - Extend `Customer.Create` with `Guid? clientManagerId = null`.
   - In `CustomerConfiguration`, map the column as
     `builder.Property(c => c.ClientManagerId);` (no FK, no nav).
2. **Backend — DTO** — add `ClientManagerId: Guid?` to `CustomerDto`,
   projection, and `ToDto`.
3. **Backend — Create/UpdateCustomer slices**
   - Commands gain `ClientManagerId: Guid?`.
   - Validators add:
     ```csharp
     When(x => x.ClientManagerId is not null, () => {
         RuleFor(x => x.ClientManagerId!.Value)
             .MustAsync(UserExists)
                 .WithError(CustomerErrors.ClientManagerNotFound)
             .MustAsync(UserIsClientManager)
                 .WithError(CustomerErrors.ClientManagerMissingRole);
     });
     ```
     where `UserIsClientManager` checks
     `uow.RepositoryFor<User>().ExistsAsync(u => u.Id == id &&
     u.RoleAssignments.Any(r => r.Role == UserRole.ClientManager), ct)`.
   - Handlers pass id into `Customer.Create` / call
     `AssignClientManager` on update.
4. **Backend — CustomerErrors** — add two error codes per file list.
5. **Backend — UserErrors** — add
   `CannotRemoveClientManagerRoleWhileAssigned` (Conflict, code
   `ERR_USER_CANNOT_REMOVE_CLIENT_MANAGER_ROLE_WHILE_ASSIGNED`).
6. **Backend — UpdateUser validator guard**
   - Helper `IsClientManagerRoleBeingRemoved(UpdateUserCommand cmd, ct)`:
     `(await GetCurrentRoles(cmd.Id, ct)).Contains(ClientManager) &&
     !cmd.Roles.Contains(ClientManager)`.
   - `MustAsync` rule on `x => x.Id` chained when the helper says yes:
     `await uow.RepositoryFor<Customer>().ExistsAsync(c =>
     c.ClientManagerId == id, ct)` must be **false**.
   - `WithError(UserErrors.CannotRemoveClientManagerRoleWhileAssigned)`.
7. **Backend — DeleteUser validator guard**
   - Same `Customers.Exists` check, gated on the user currently
     having `ClientManager`. Reuses the same error code.
8. **Backend — GetUsers role filter**
   - Add optional `Role` to `GetUsersQuery`.
   - Handler: when set, query is filtered before
     `GetAllAsDtosAsync<UserDto>` — easiest path is to switch to
     `repo.WhereAsDtosAsync<UserDto>(u =>
     u.RoleAssignments.Any(r => r.Role == role), ct)` or equivalent;
     fall back to in-memory filter if the existing helper doesn't
     accept a predicate.
   - Endpoint reads `?role=` and parses via
     `Enum.TryParse<UserRole>(roleParam, true, out var role)`.
9. **Backend — EF migration** — run `bun --filter api ef:add
   CustomerClientManager` (or the documented command from
   `ef-migration` skill); confirm it just adds the column.
10. **Backend tests** — extend builders, handler tests, validator
    tests, integration tests per file list. Use `backend-unit-test`
    and `backend-integration-test` skills.
11. **Schema regen** — `bun --filter web gen:api`.
12. **Frontend — server fns** — add `fetchUsersByRole`; existing
    customers server fns map the new field both ways.
13. **Frontend — form schemas** — extend `customerFormSchema` with
    `clientManagerId`.
14. **Frontend — customer create form** — wrap the form in a
    `useQuery(['client-managers'], () =>
    fetchUsersByRole({ data: { role: 'ClientManager' } }))`; render a
    `ComboboxField` whose options are
    `[{ value: '', label: '(none)' }, ...users.map(u => ({ value:
    u.id, label: \`${u.firstName} ${u.lastName}\` }))]`.
15. **Frontend — customer edit card** — same wiring; initial value
    from `customer.clientManagerId ?? ''`.
16. **Manual verification** — start `bun --filter web dev` + API,
    create a customer with no manager, create with one, edit to
    switch / clear, attempt to remove `ClientManager` role from an
    assigned user (expect inline 409 banner), unassign then retry
    (expect success), repeat for delete.

## Tests

- **Unit (customer):**
  - `CreateCustomerHandler` accepts `null` and valid id; persists it.
  - Validator rejects unknown `ClientManagerId` (NotFound).
  - Validator rejects user that exists but lacks the role
    (Validation).
  - `UpdateCustomerHandler` swaps and clears `ClientManagerId`.
- **Unit (user):**
  - `UpdateUserValidator` allows changing roles when nothing is
    linked.
  - `UpdateUserValidator` rejects removing `ClientManager` when a
    non-deleted customer is still linked (Conflict).
  - `UpdateUserValidator` allows removing when only soft-deleted
    customers are linked.
  - Same three cases for `DeleteUserValidator`.
  - `GetUsersHandler` filters by role when set; returns all when
    unset.
- **Integration:**
  - POST /api/customers with valid `clientManagerId` → 201, body
    reflects it.
  - POST /api/customers with non-ClientManager user id → 400 with
    `ERR_CUSTOMER_CLIENT_MANAGER_MISSING_ROLE`.
  - PUT /api/customers/{id} clears the assignment by sending `null`.
  - PUT /api/users/{id} removing `ClientManager` while linked → 409.
  - DELETE /api/users/{id} while linked → 409.
  - GET /api/users?role=ClientManager returns the subset.

## Edge Cases

- **Customer soft-delete after assignment** — the query filter
  excludes those rows from the "is the user still linked" check, so
  soft-deleting a customer frees its assigned manager. Document this
  explicitly in the integration test.
- **Switching the assigned user instead of removing the role** — no
  guard triggers; the customer simply ends up pointing at a new
  `ClientManagerId`. The old user keeps the role; nothing to clean up.
- **Editing other fields while a customer references an invalid
  user** — possible if a user is hard-deleted out-of-band. Out of
  scope; we don't hard-delete users.
- **Assigning a `ClientManager` who is then soft-deleted by another
  admin** — the customer keeps the (now invalid) FK until updated.
  Acceptable v1 behavior; the soft-delete guard prevents the common
  case.
- **Self-assignment** — admin can assign themselves to a customer if
  they hold `ClientManager`. No guard.
- **Race**: two admins, one removes the role while the other assigns
  a new customer to the same user — last-write-wins per request,
  validators each check at request time. Worst case: brief
  inconsistency until the next write. Acceptable; documenting in
  edge cases rather than adding locking.
- **`GetUsers` returns soft-deleted users when filtered by role** —
  global query filter on `User` excludes soft-deleted, so the picker
  naturally hides them.

## Assumptions

- One ClientManager per Customer (not a list).
- ClientManager assignment lives on `Customer`, not on `User`
  (one-to-many from user → customers).
- No DB-level FK constraint between Customers.ClientManagerId and
  Users.Id — modules stay decoupled at the schema level.
- No notification or audit log when an admin tries to remove a role
  and is blocked — surface the error inline; don't email anyone.
- A user with `ClientManager` role but zero customers is fine (the
  role isn't proof of assignment).
- Frontend customer list does not (yet) show the assigned manager's
  name. Defer that column to a follow-up to avoid an extra
  per-row lookup join in v1.
