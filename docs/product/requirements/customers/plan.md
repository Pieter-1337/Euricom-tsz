# Plan: Customers module — full CRUD per requirements

## Goal

Extend the existing `Customers` module from `{ Id, Name }` to the full requirements set — auto-generated sequential number, `Address` value object (street/zip/city/country, all optional), `ContactPerson` value object (name optional, email required) — with admin CRUD parity to the Users module, a paged endpoint, and a Users-style list (`ListShell` + `useListQuery` + sortable columns + infinite scroll + search + soft-delete filter). Prio 2 (client manager) is out of scope.

## Context

- **Existing backend stub:** `Customer { Id, Name }`, single `GetCustomersQuery` + `GET /api/customers` (admin-only). Migration `20260519092345_AddCustomers` already created the bare `Customers` table.
- **Existing frontend stub:** `customers-list.tsx` (Id + Name table) wired at `_protected/admin/customers/index.tsx`. Nav link already present in the admin sidebar (`_protected.tsx`).
- **Reference template:** Users module — DDD entity with `Create` factory, private setters, named mutators; `IEntityDto<TEntity, TDto>` with `Project` expression; one CQRS slice per file; FluentValidation on writes; soft delete via `DeletedAt` + EF query filter; `RequireAdmin` admin sub-group; `GetUsersPaged` with `KeysetQueryOptions` + `SortMap` + `Searchable` expressions; web list using `useListQuery` + `ListShell` + `SortableHeaderCell`.
- **No prior value-object precedent in the codebase** — Address + ContactPerson establish the pattern: C# `record` (reference type), private parameterless ctor (for EF), static `Create` factory that trims and normalizes empty → null, static `Empty`.
- **DB:** SQLite, no sequences → sequential `Number` computed `max(Number)+1` in `CreateCustomerHandler` under a unique index.
- **Web:** TanStack Router file-based routes, TanStack Form + zod + shadcn (`useAppForm` base) — see `/admin/users`.

## Files

### Backend — `packages/api/Tsz.Api/Modules/Customers/`

- `Address.cs` — **new**: `public sealed record Address` with `Street/Zip/City/Country` (all `string?`); private parameterless ctor (EF), static `Create(street?, zip?, city?, country?)` that trims and converts empty → null; static `Empty`.
- `ContactPerson.cs` — **new**: `public sealed record ContactPerson` with `Name` (`string?`) + `Email` (required, non-null); private parameterless ctor; static `Create(name?, email)`.
- `Customer.cs` — **modify**: add `Number` (int, immutable post-create), `Address Address` (non-null, default `Address.Empty`), `ContactPerson ContactPerson` (non-null), `DeletedAt`; replace `Create(name)` with `Create(name, contactEmail, number, address?, contactName?)`; named mutators: `Rename`, `UpdateAddress(Address)`, `UpdateContact(ContactPerson)`, `SoftDelete(at)`.
- `CustomerConfiguration.cs` — **modify**: `HasQueryFilter(c => c.DeletedAt == null)`; required: `Name`, `Number`; unique index on `Number`; `OwnsOne(c => c.Address, ...)` with `Address_Street/Zip/City/Country` (nullable, max-lengths); `OwnsOne(c => c.ContactPerson, ...)` with `ContactPerson_Name` (nullable) + `ContactPerson_Email` (required).
- `CustomerDto.cs` — **modify**: `CustomerDto(Guid Id, int Number, string Name, AddressDto Address, ContactPersonDto ContactPerson)`. Nested `AddressDto(string? Street, string? Zip, string? City, string? Country)` and `ContactPersonDto(string? Name, string Email)`. Update `Project` + `ToDto`.
- `CustomerErrors.cs` — **new**: `CustomerErrors.NotFound`.
- `Features/GetCustomers.cs` — **keep** as-is (used internally; nav still has unpaged list as a fallback / older endpoint kept). Returns updated DTO via `Project`.
- `Features/GetCustomerById.cs` — **new**: `GetCustomerByIdQuery(Guid Id) : IQuery<CustomerDto?>`; null on miss.
- `Features/GetCustomersPaged.cs` — **new**: `GetCustomersPagedQuery(...) : KeysetQueryOptions(...), IQuery<KeysetPage<CustomerDto>>`; sort map covers `number`, `name`, `city`, `contactEmail`; searchable includes Name + ContactPerson.Email + ContactPerson.Name + Address.City.
- `Features/CreateCustomer.cs` — **new**: command `CreateCustomerCommand(string Name, ContactPersonDto ContactPerson, AddressDto? Address)`; validator (Name required + max, ContactPerson.Email required + EmailAddress + max, optional ContactPerson.Name max-length, optional Address.* max-lengths); handler computes `Number = (max ?? 0) + 1`, constructs `Address.Create(...)` and `ContactPerson.Create(...)`, calls `Customer.Create(...)`, returns DTO.
- `Features/UpdateCustomer.cs` — **new**: command `UpdateCustomerCommand(Guid Id, string Name, ContactPersonDto ContactPerson, AddressDto? Address)`; handler loads, applies named mutators, returns DTO. `Number` immutable.
- `Features/DeleteCustomer.cs` — **new**: `DeleteCustomerCommand(Guid Id)`; soft delete via `TimeProvider`.
- `CustomerEndpoints.cs` — **modify**: admin group; routes: `GET /` (unpaged, existing), `GET /paged`, `GET /{id:guid}` (named `GetCustomerById`), `POST /` (`CreatedAtRoute`), `PUT /{id:guid}` (route-id check), `DELETE /{id:guid}`. `ValidationFilter<T>` on POST/PUT.
- `Persistence/Migrations/<timestamp>_ExpandCustomers.cs` — **new**: add `Number` + unique index, `Address_*` (nullable), `ContactPerson_Name` (nullable), `ContactPerson_Email` (required), `DeletedAt` (nullable). Generated via `ef-migration` skill.

### Backend — tests

- `packages/api/tests/Tsz.Api.Tests/Builders/CustomerBuilder.cs` — **new**: NBuilder-style with-extensions.
- `packages/api/tests/Tsz.Api.Tests/Modules/Customers/*HandlerTests.cs` — **new**: one per slice (`GetCustomers`, `GetCustomerById`, `GetCustomersPaged`, `Create`, `Update`, `Delete`).
- `packages/api/tests/Tsz.Api.Tests/Modules/Customers/CreateCustomerValidatorTests.cs` + `UpdateCustomerValidatorTests.cs` — **new**.
- `packages/api/tests/Tsz.Api.Tests.Integration/CustomerEndpointsTests.cs` — **new**.

### Frontend — `packages/web/`

- `src/api/customers.ts` — **modify**: re-export `Customer = components['schemas']['CustomerDto']`, `Address = components['schemas']['AddressDto']`, `ContactPerson = components['schemas']['ContactPersonDto']`, `CreateCustomerCommand`, `UpdateCustomerCommand`. Wrappers `getCustomers`, `getCustomersPaged`, `getCustomerById` (404 → null), `createCustomer`, `updateCustomer`, `removeCustomer`.
- `src/api/customers.server.ts` — **modify**: server counterparts mirroring `users.server.ts`.
- `src/api/schema.ts` — **regenerate** via `bun --filter web gen:api`.
- `src/features/customers/schemas.ts` — **new**: zod schemas + `CustomerSortKey` type + paged-params schema, mirroring `features/users/schemas.ts`.
- `src/features/customers/server-fns.ts` — **modify**: `fetchCustomersPaged`, `fetchCustomer`, `submitCreateCustomer`, `saveCustomer`, `deleteCustomer`.
- `src/features/customers/components/customers-list.tsx` — **rewrite**: `useListQuery<Customer, CustomerSortKey>` + `ListShell` with columns Number / Name / City / Contact email; rows click → `$id`.
- `src/features/customers/components/customer-form.tsx` — **new**: shared `useAppForm` + zod schema, flat fields in UI assembled into nested address/contactPerson on submit.
- `src/routes/_protected/admin/customers/index.tsx` — keep.
- `src/routes/_protected/admin/customers/new.tsx` — **new**.
- `src/routes/_protected/admin/customers/$id.tsx` — **new**: edit + delete; Number rendered read-only.

## Steps

1. Address + ContactPerson value objects.
2. Entity + config + DTO + errors.
3. CQRS slices (GetById, GetPaged, Create, Update, Delete). Keep GetCustomers.
4. Endpoints.
5. EF migration.
6. Backend tests (unit + integration).
7. Build API + regen TS schema.
8. Frontend API layer + server-fns + schemas.
9. Frontend list (users-style) + form + new/edit routes.
10. Smoke.

## Tests

- **Unit:** handler tests per slice (Number auto-increments, mutators applied, soft-delete sets DeletedAt). Validator tests (empty/missing/bad-email/over-max).
- **Integration:** all endpoints, admin gate (403), `IgnoreQueryFilters` confirms soft delete is preserved in DB.

## Edge Cases

- Concurrent creates colliding on `Number` — unique index will raise on race. App scale doesn't justify a retry loop; document only.
- Empty-string vs null in optional fields — `Address.Create` / `ContactPerson.Create` trim and convert empty → null at the boundary.
- Always-present VOs in domain + always-present nested objects in DTO — JSON will always have `address` + `contactPerson`, possibly with null inner fields. Stable for clients.

## Assumptions

- `Number` is a plain `int`; displayed unformatted.
- `GET /api/customers` (unpaged) is kept for back-compat though list UI uses `/paged`.
- No `IMPLEMENTATION_STATUS.md` doc unless requested.
