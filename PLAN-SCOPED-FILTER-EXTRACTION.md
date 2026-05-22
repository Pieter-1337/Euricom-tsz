# Plan — Extract scoped-filter composition shared by validators and handlers

## Context — what is already shipped

This plan **follows** a refactor that introduced `IDataScopeAccessor` and made list/detail/write endpoints respect a `ClientManager` ownership scope. Before opening this plan, the next session should know:

### Authorization wiring (in place)

- Roles: `User`, `Admin`, `ClientManager` (enum in `packages/api/Modules/Users/Tsz.Modules.Users.Contracts/UserRole.cs`).
- Backend policy `RequireClientManager` (Admin OR ClientManager) on Customers + Contracts endpoint groups. `RequireAdmin` stays on Users endpoints.
- Frontend `/_protected/admin` route allows Admin or ClientManager; `/_protected/admin/users` is Admin-only. Sidebar shows Customers/Contracts for both roles, Users for Admin only.

### Auth-infrastructure layout (in place)

- `Tsz.Infrastructure.Auth/ICurrentUserResolver.cs` — returns a thin `ResolvedUser(Guid Id, IReadOnlyCollection<string> RoleNames)` with a `HasRole(name)` helper.
- `Tsz.Infrastructure.Auth/IDataScopeAccessor.cs` — `Task<Expression<Func<T, bool>>?> OwnershipFilterAsync<T>(OwnershipPolicy<T>, CancellationToken)`. `null` means full access; non-null is an expression to AND into the query; a `_ => false` predicate is returned when no caller is resolvable.
- `Tsz.Infrastructure.Auth/OwnershipPolicy.cs` — `record OwnershipPolicy<T>(Expression<Func<T, Guid?>> OwnerIdSelector, IReadOnlyCollection<string> FullAccessRoles)`. Role names as strings (use `nameof(UserRole.Admin)` at the call site) to keep Infrastructure free of the `UserRole` enum dependency.
- `Tsz.Modules.Users.Auth/ICurrentUserAccount.cs` — Users-module-internal interface that returns the full `User?` aggregate (used by `GetCurrentUserHandler`).
- `Tsz.Modules.Users.Auth/CurrentUserResolver.cs` — single class implementing **both** `ICurrentUserResolver` and `ICurrentUserAccount` with a shared per-request cache. Registered in DI as scoped (one instance, two interfaces, both resolving to the same instance via factory delegates in `UsersModule.cs`).
- `Tsz.Modules.Users.Auth/DataScopeAccessor.cs` — impl of `IDataScopeAccessor`. Wraps `ICurrentUserResolver`. Builds the owner-equals-userId `Expression<Func<T, bool>>` via `Expression.Equal(selector.Body, Expression.Constant((Guid?)userId, typeof(Guid?)))`.

### What the scope check is applied to today (the repetition this plan targets)

Each handler/validator below currently inlines the same 4-line block:

```csharp
var ownership = await scope.OwnershipFilterAsync(<EntityHandler>.ScopePolicy, ct);
Expression<Func<TEntity, bool>> filter = ownership is null
    ? (e => additionalPredicate(e))
    : ownership.And(e => additionalPredicate(e));
return await repo.<Method>(filter, ct);
```

**Handlers (3):**
- `Modules/Customers/.../Features/GetCustomerById.cs` → `FirstOrDefaultAsDtoAsync<CustomerDto>(filter)`
- `Modules/Contracts/.../Features/GetContractById.cs` → `FirstOrDefaultAsDtoAsync<ContractDto>(filter)`
- `Modules/Customers/.../CrossModule/CustomerExistsQueryHandler.cs` → `ExistsAsync(filter)` (cross-module; called by `CreateContractValidator`)

**Validators (4):**
- `Modules/Customers/.../Features/UpdateCustomer.cs` → `CustomerExistsAndAccessible` private method
- `Modules/Customers/.../Features/DeleteCustomer.cs` → `CustomerExistsAndAccessible` private method
- `Modules/Contracts/.../Features/UpdateContract.cs` → `ContractExistsAndAccessible` private method
- `Modules/Contracts/.../Features/DeleteContract.cs` → `ContractExistsAndAccessible` private method

**Paged handlers (2) — slightly different shape** (they pass the ownership filter directly into `GetPagedAsync` without `.And`-ing an id filter, but the `await scope.OwnershipFilterAsync(...)` call still recurs):
- `Modules/Customers/.../Features/GetCustomersPaged.cs`
- `Modules/Contracts/.../Features/GetContractsPaged.cs`

**Non-ownership rule that also repeats (3 places):** "non-admin must (be self-assigned / can't reassign manager)" — uses `ICurrentUserResolver` directly. Lives in:
- `CreateCustomerValidator.NonAdminAssignsSelf`
- `UpdateCustomerValidator.NonAdminCannotReassignManager`
- `UpdateContractValidator.NonAdminCannotReassignManager`

### Existing static `ScopePolicy` per entity

Currently shaped around an `Expression<Func<T, Guid?>>` selector that `DataScopeAccessor` rewrites into an equality expression at runtime:

```csharp
// GetCustomersPagedHandler
internal static readonly OwnershipPolicy<Customer> ScopePolicy = new(
    OwnerIdSelector: c => c.ClientManagerId,
    FullAccessRoles: [nameof(UserRole.Admin)]);

// GetContractsPagedHandler — same shape, against Contract.ClientManagerId
```

**This refactor reshapes them to delegate factories** (see *Prerequisite* in "What to add" below) so no `Expression.Equal` / `Expression.Lambda` construction happens at runtime.

---

## Goal of this refactor

1. Pull the `await OwnershipFilterAsync(...) → null ? a : ownership.And(b)` composition into **one** place so handlers and validators share the implementation rather than re-deriving it.
2. Give command **and** query validators that participate in ownership/self-assignment a discoverable base class (mirrors the *spirit* of E-loket's `UserValidator<T>` — declarative, inheritance-based — but adapted to our entity-scoped shape rather than their per-command role lists).
3. Do **not** introduce a per-command role-gate base class. ASP.NET Core endpoint policies already gate role access at the request pipeline; a second validator-level role gate would be redundant in our codebase (this differs from E-loket, which does not use policy-based authorization).

Non-goals:
- Lifting domain invariants out of validators into the domain layer. That is a separate, larger conversation about `Result<T>` + `DomainError` types, deliberately deferred — see "Open follow-ups" below.
- Introducing per-command role groups à la E-loket.
- Adding integration test coverage for non-Admin callers (would require `TestAuthHandler` to authenticate as the seeded user; separate change).

---

## What to add

### Prerequisite — reshape `OwnershipPolicy<T>` + `DataScopeAccessor` to use a delegate factory

The current shape forces `DataScopeAccessor` to compose `selector == userId` via `Expression.Equal` + `Expression.Lambda` at runtime. Move the equality construction into the policy declaration itself — the compiler then emits the expression tree from a normal C# lambda at the call site, and `DataScopeAccessor` becomes a one-liner that simply invokes the factory.

**`Tsz.Infrastructure.Auth/OwnershipPolicy.cs`:**

```csharp
public record OwnershipPolicy<T>(
    Func<Guid, Expression<Func<T, bool>>> OwnerEquals,
    IReadOnlyCollection<string> FullAccessRoles);
```

**`Tsz.Modules.Users.Auth/DataScopeAccessor.cs`** — drop the `Expression.Equal` / `Expression.Lambda` block:

```csharp
public async Task<Expression<Func<T, bool>>?> OwnershipFilterAsync<T>(
    OwnershipPolicy<T> policy, CancellationToken ct)
{
    var user = await currentUser.ResolveAsync(ct);
    if (user is null) return _ => false;
    if (policy.FullAccessRoles.Any(user.HasRole)) return null;
    return policy.OwnerEquals(user.Id);
}
```

**Paged-handler `ScopePolicy` declarations** become:

```csharp
// GetCustomersPagedHandler
internal static readonly OwnershipPolicy<Customer> ScopePolicy = new(
    OwnerEquals: userId => c => c.ClientManagerId == userId,
    FullAccessRoles: [nameof(UserRole.Admin)]);

// GetContractsPagedHandler — same shape, against Contract.ClientManagerId
```

Trade-off: one extra `userId =>` per policy declaration, zero runtime expression construction, full compile-time type-checking of the predicate.

### 1. `Tsz.Infrastructure.Auth.Validation/ScopedFilter.cs`

Static helper used by both validators and handlers. Single source of truth for the compose-with-ownership operation.

```csharp
namespace Tsz.Infrastructure.Auth.Validation;

public static class ScopedFilter
{
    /// Resolves the caller's ownership filter for <paramref name="policy"/> and AND-combines
    /// it with <paramref name="additional"/>. When the caller has full access (e.g. Admin),
    /// returns <paramref name="additional"/> unchanged.
    public static async Task<Expression<Func<T, bool>>> ComposeAsync<T>(
        IDataScopeAccessor scope,
        OwnershipPolicy<T> policy,
        Expression<Func<T, bool>> additional,
        CancellationToken ct = default)
    {
        var ownership = await scope.OwnershipFilterAsync(policy, ct);
        return ownership is null ? additional : ownership.And(additional);
    }
}
```

Notes:
- Lives in `Tsz.Infrastructure.Auth.Validation` namespace (new subnamespace under existing `Tsz.Infrastructure.Auth`). The `.Validation` segment is the home for both this helper and `ScopedRequestValidator<T>`.
- Uses the existing `PredicateBuilder.And` extension from `Tsz.Infrastructure.Common.Pagination`. The new file should `using` that namespace.

### 2. `Tsz.Infrastructure.Auth.Validation/ScopedRequestValidator.cs`

Abstract base class. **Generic in request type — covers commands and queries.**

```csharp
namespace Tsz.Infrastructure.Auth.Validation;

public abstract class ScopedRequestValidator<TRequest>(
    IUnitOfWork uow,
    IDataScopeAccessor scope,
    ICurrentUserResolver currentUser)
    : AbstractValidator<TRequest>
{
    protected readonly IUnitOfWork Uow = uow;
    protected readonly IDataScopeAccessor Scope = scope;
    protected readonly ICurrentUserResolver CurrentUser = currentUser;

    /// Validates that the property's value identifies an existing row of <typeparamref name="TEntity"/>
    /// that is accessible to the caller per <paramref name="policy"/>.
    /// <paramref name="idEqualsFactory"/> constructs `e => e.Id == id` (or equivalent) for the entity
    /// — written as a normal C# lambda at the call site, no runtime expression building.
    protected IRuleBuilderOptions<TRequest, Guid> RuleForOwnedEntity<TEntity>(
        Expression<Func<TRequest, Guid>> property,
        OwnershipPolicy<TEntity> policy,
        Func<Guid, Expression<Func<TEntity, bool>>> idEqualsFactory)
        where TEntity : class
    {
        return RuleFor(property).MustAsync(async (id, ct) =>
        {
            var filter = await ScopedFilter.ComposeAsync(Scope, policy, idEqualsFactory(id), ct);
            return await Uow.RepositoryFor<TEntity>().ExistsAsync(filter, ct);
        });
    }

    /// Validates that for non-admin callers, the property equals the caller's own user id.
    /// Admins are unrestricted. Returns false when no caller is resolvable.
    protected IRuleBuilderOptions<TRequest, Guid?> RuleForSelfAssignedManager(
        Expression<Func<TRequest, Guid?>> property)
    {
        return RuleFor(property).MustAsync(async (value, ct) =>
        {
            var user = await CurrentUser.ResolveAsync(ct);
            if (user is null) return false;
            if (user.HasRole(nameof(UserRole.Admin))) return true;
            return value == user.Id;
        });
    }
}
```

Notes:
- `UserRole` is referenced via `nameof(UserRole.Admin)` — Infrastructure does **not** reference the enum directly. The base class lives in Infrastructure; consumers (which already reference `Tsz.Modules.Users.Contracts`) supply the role name through this string-based check. If Infrastructure cannot reference `nameof(UserRole.Admin)` directly (because `UserRole` is in Users.Contracts), the literal `"Admin"` is acceptable — the same convention `RequireAdminAuthorizationHandler` already uses.
  - **Decide at implementation time:** either lift the string to a constant in `Tsz.Infrastructure.Auth/AuthorizationPolicies.cs` (e.g. `public const string AdminRoleName = "Admin"`) or accept the literal. Constant is cleaner.
- Properties (`Uow`, `Scope`, `CurrentUser`) are exposed as `protected readonly` so subclass-specific rules (e.g. `UserExistsQuery` lookups) can still reuse the injected services without re-injecting.

---

## Migration — which existing files change

### Query handlers (use `ScopedFilter.ComposeAsync` directly)

- `Modules/Customers/.../Features/GetCustomerById.cs` — replace the 4-line block with one `ScopedFilter.ComposeAsync` call.
- `Modules/Contracts/.../Features/GetContractById.cs` — same.
- `Modules/Customers/.../CrossModule/CustomerExistsQueryHandler.cs` — same.

The two paged handlers (`GetCustomersPaged`, `GetContractsPaged`) keep their direct `scope.OwnershipFilterAsync` call — they pass the result as the `filter` arg to `GetPagedAsync` and never compose it with another predicate, so they wouldn't benefit from `ComposeAsync`. **Leave them as-is.**

### Validators (inherit `ScopedRequestValidator<TRequest>`)

For each of the four validators below, change the base class and replace the inline private method with `RuleForOwnedEntity`. Existing constructor-injected deps that the base class now owns (`IUnitOfWork`, `IDataScopeAccessor`, `ICurrentUserResolver`) get forwarded via `: base(uow, scope, currentUser)`. Subclass-only deps (`IUsersAccessModule`) stay on the subclass.

Call-site shape (note `id => c => c.Id == id` is a plain C# lambda — the compiler builds the expression tree):

```csharp
RuleForOwnedEntity(x => x.Id, GetCustomersPagedHandler.ScopePolicy, id => c => c.Id == id);
```

- `Modules/Customers/.../Features/UpdateCustomer.cs` — inherits base; uses `RuleForOwnedEntity` for existence; uses `RuleForSelfAssignedManager` for the non-admin-reassign rule.
- `Modules/Customers/.../Features/DeleteCustomer.cs` — inherits base; uses `RuleForOwnedEntity`. No other rules need scope.
- `Modules/Contracts/.../Features/UpdateContract.cs` — same pattern as UpdateCustomer.
- `Modules/Contracts/.../Features/DeleteContract.cs` — same pattern as DeleteCustomer.

`CreateCustomerValidator` keeps its own structure (no entity to look up — there's no existing record yet) but switches its `NonAdminAssignsSelf` check to `RuleForSelfAssignedManager` for consistency. It does **not** need to inherit `ScopedRequestValidator` since it has neither `IUnitOfWork` nor `IDataScopeAccessor` dependencies — passing all three when only one is used would be noise. Acceptable inconsistency; the rule extension is the abstraction, the base class is bonus.

**Optional decision point:** if you'd rather have *every* customer/contract validator inherit `ScopedRequestValidator` for marker-class consistency (even when only `RuleForSelfAssignedManager` is used), accept that `CreateCustomerValidator` ends up injecting `IUnitOfWork` + `IDataScopeAccessor` it doesn't use. My call would be: don't. The base class earns its place by collapsing real duplication, not by being decorative.

### Reshape of already-shipped files (per *Prerequisite* above)

- `Tsz.Infrastructure/Auth/OwnershipPolicy.cs` — `OwnerIdSelector` → `OwnerEquals` (factory).
- `Modules/Users/.../Auth/DataScopeAccessor.cs` — drop the `Expression.Equal` block; invoke factory.
- `Modules/Customers/.../Features/GetCustomersPaged.cs` — `ScopePolicy` declaration updated to factory form.
- `Modules/Contracts/.../Features/GetContractsPaged.cs` — same.

### What does **not** change

- DI registrations in `UsersModule.cs` — `IDataScopeAccessor` and `ICurrentUserResolver` are already registered; subclasses pull them through normal DI.
- The location of `ScopePolicy` declarations — they stay on the paged handlers as canonical definitions, referenced by name from validators and detail handlers. Only their *shape* changes.
- The Users module's `RequireAdmin` / `RequireClientManager` authorization handlers — those operate at the endpoint pipeline level, unrelated to this refactor.

---

## Test impact

All 252 unit + 107 integration tests pass against the current implementation. After this refactor:

### Tests that need updating (fixture-only, no behavior change)

- `Tsz.Api.Tests/Modules/Customers/Features/UpdateCustomerValidatorTests.cs`
- `Tsz.Api.Tests/Modules/Customers/Features/DeleteCustomerValidatorTests.cs` — does not exist today; **no change** unless one is added (the existing inline check is currently covered indirectly by integration tests).
- `Tsz.Api.Tests/Modules/Customers/Features/CreateCustomerValidatorTests.cs`
- `Tsz.Api.Tests/Modules/Contracts/Features/UpdateContractValidatorTests.cs`
- `Tsz.Api.Tests/Modules/Contracts/Features/DeleteContractValidatorTests.cs`

The fixture changes are mechanical: where the test currently calls `new XValidator(uow.Object, users.Object, scope.Object, resolver.Object)`, the call site is identical (the base constructor takes the same three args; the subclass-specific arg stays where it was). The mocks should already exist from the prior refactor. Effectively a no-op for tests **unless** the constructor parameter order changes — if it does, update each test's `BuildValidator` helper accordingly.

### Tests for the new helpers

Add two small test files:

- `Tsz.Api.Tests/Infrastructure/Auth/Validation/ScopedFilterTests.cs` — verifies `ComposeAsync` returns `additional` unchanged when scope is null, and `ownership.And(additional)` otherwise. Mock `IDataScopeAccessor`.
- `Tsz.Api.Tests/Infrastructure/Auth/Validation/ScopedRequestValidatorTests.cs` — derives a tiny test-only subclass and verifies `RuleForOwnedEntity` calls `ExistsAsync` with the composed filter, and `RuleForSelfAssignedManager` accepts/rejects based on `ResolvedUser.HasRole(Admin)`.

### Tests that should continue to pass without modification

- All `GetXPagedHandlerTests` — paged handlers are not touched.
- `GetCustomerByIdHandlerTests` / `GetContractByIdHandlerTests` — the captured-filter assertion (compile + apply to `owned` / `foreign` / `wrong-id` entities) is shape-agnostic. The `ScopedFilter.ComposeAsync` helper should produce an expression that satisfies the same assertions.
- All integration tests — endpoint behavior is unchanged.

---

## Implementation order

Sequential; each step buildable + testable independently.

1. **Reshape `OwnershipPolicy<T>`** — switch `OwnerIdSelector` to `OwnerEquals` factory. Update both paged-handler `ScopePolicy` declarations in the same step (the type change forces it). Simplify `DataScopeAccessor.OwnershipFilterAsync` to invoke the factory. Run tests — all should still pass; the resulting expression tree is shape-equivalent to what `Expression.Equal` produced.
2. **Add `ScopedFilter.ComposeAsync`** in a new file under `Tsz.Infrastructure/Auth/Validation/`. Build runs green; no consumers yet.
3. **Migrate the three query handlers** (`GetCustomerById`, `GetContractById`, `CustomerExistsQueryHandler`) to use `ScopedFilter.ComposeAsync`. Run unit tests; existing tests should pass unchanged.
4. **Add `ScopedRequestValidator<TRequest>`** base class. Build green; no consumers yet.
5. **Migrate `DeleteCustomerValidator`** first — simplest case (one rule, no other concerns). Validate the inheritance shape works.
6. **Migrate `DeleteContractValidator`** — copy of (5) against the Contract entity.
7. **Migrate `UpdateCustomerValidator` and `UpdateContractValidator`** — they additionally need `RuleForSelfAssignedManager`.
8. **Migrate `CreateCustomerValidator`** to use `RuleForSelfAssignedManager` (no inheritance — see decision point above).
9. **Add new tests** for `ScopedFilter` and `ScopedRequestValidator`.
10. **Run full test suite** (`dotnet test Tsz.Api.Tests` and `Tsz.Api.Tests.Integration`) — expect 252 + new + 107 all green.

Each step is a candidate commit boundary.

---

## Open follow-ups (NOT part of this refactor — flagged for later)

1. **Domain invariants → domain layer.** Today validators mirror single-aggregate invariants (e.g. `Name.NotEmpty().MaximumLength(256)`) that `Customer.Rename` / `Contract.UpdatePeriod` already enforce. A future refactor could introduce `Result<T>` + `DomainError` types, lift invariants to typed domain results, and decide whether to keep validator mirrors (fast-feedback) or drop them (single source of truth). This is the conversation the user started with "perhaps we shouldn't put this kind of logic in the validators anyway but keep that at the domain" — deferred deliberately because it's a meaningful refactor with its own design questions.

2. **Integration tests for non-Admin paths.** `TestAuthHandler` (`Tsz.Api.Tests.Integration/TestAuth/TestAuthHandler.cs`) hardcodes a single "test-user-id" caller. To exercise the scope filters end-to-end against a seeded ClientManager, the handler needs to read the intended caller from test context (per-test-class scheme registration, or a header-driven impersonation hook). The unit-test layer covers the policy logic; integration tests today only cover the Admin happy path because every test seeds an Admin user matching the hardcoded OID.

3. **Identity module split.** `ICurrentUserResolver` is in Infrastructure but its implementation (`CurrentUserResolver` + `DataScopeAccessor`) lives in the Users module because it reads the User table. A future move could extract a dedicated `Tsz.Modules.Identity` slice with its own `IdentityPrincipal` view (and possibly its own table), decoupling "who is authenticated" from "user-management CRUD." Not blocking anything today.

4. **Write-endpoint integration tests for the new ownership rules.** The unit tests verify the validator+handler logic in isolation. End-to-end coverage (POST/PUT/DELETE under non-Admin caller) requires the same `TestAuthHandler` change as (2).

---

## Quick reference for the next session

**Where things live (file paths, absolute):**

- Interface contracts: `C:\Users\PieterBracke\git\tsz\packages\api\Tsz.Infrastructure\Auth\`
- Users-module auth impl: `C:\Users\PieterBracke\git\tsz\packages\api\Modules\Users\Tsz.Modules.Users\Auth\`
- Customers feature files: `C:\Users\PieterBracke\git\tsz\packages\api\Modules\Customers\Tsz.Modules.Customers\Features\`
- Contracts feature files: `C:\Users\PieterBracke\git\tsz\packages\api\Modules\Contracts\Tsz.Modules.Contracts\Features\`
- Unit tests root: `C:\Users\PieterBracke\git\tsz\packages\api\Tsz.Api.Tests\`
- Integration tests root: `C:\Users\PieterBracke\git\tsz\packages\api\Tsz.Api.Tests.Integration\`

**Build + test commands:**

```pwsh
# from packages/api/
dotnet build Tsz.Api.Tests/Tsz.Api.Tests.csproj
dotnet test Tsz.Api.Tests/Tsz.Api.Tests.csproj --no-build
dotnet test Tsz.Api.Tests.Integration/Tsz.Api.Tests.Integration.csproj
```

**Last green-test state captured:** 252 unit + 107 integration, on the commit immediately preceding this plan.
