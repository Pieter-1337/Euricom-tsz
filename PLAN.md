# Plan: Dispatcher + Typed Error Codes + ProblemDetails

## Goal

Replace ad-hoc handler return shapes (`CreateUserResult.Conflict`, nullable DTOs, custom `Found/HasReferences` records, plain `bool`) with: an in-house dispatcher that enforces a single `ValidationBehavior`, per-module typed `ErrorCode` SmartEnums, validators that own business-rule checks, and a global exception handler that maps `ValidationException` to RFC 7807 ProblemDetails with semantic HTTP status — giving the frontend one uniform error shape it can surface in toasts.

## Context

- **Custom CQRS, no MediatR.** Handlers implement `ICommandHandler<TCommand, TResponse>` / `IQueryHandler<TQuery, TResponse>` (`packages/api/Tsz.Infrastructure/Abstractions/`). Auto-registered via `AddHandlersFromAssembly`. Endpoints call `handler.HandleAsync(...)` directly today.
- **Validation today is opt-in per endpoint** via `.AddEndpointFilter<ValidationFilter<T>>()` (`packages/api/Tsz.Infrastructure/Validation/ValidationFilter.cs`) — forget the filter and validation silently skips.
- **Transactions** — `EfCoreUnitOfWork` exposes only `RepositoryFor<T>` + `SaveChangesAsync` (depth-tracked Begin/Close API removed in the preceding cleanup commit). Atomicity = one `SaveChangesAsync` per handler under EF's implicit txn.
- **No global exception handler.** Unhandled exceptions hit ASP.NET Core's default.
- **Current failure shapes are inconsistent** — `CreateUser.cs:34-35` returns `CreateUserResult { Conflict = true }`; `UpdateUser.cs:25` returns `null`; `DeleteUser.cs:14` returns `bool`; `DeleteLeaveType.cs:8-12` returns custom `(Found, HasReferences)`. Each endpoint hand-rolls the HTTP mapping (`Results.Conflict(new { error = "..." })`, `Results.NotFound()`).
- **Frontend** — `packages/web/src/api/client.ts` defines `ApiRequestError` carrying only `status`. No body parsing.
- **Reference** — DDD repo at `C:/Users/PieterBracke/git/DDD` uses typed `ErrorCode` SmartEnums (`BuildingBlocks.Enumerations/ErrorCode.cs`) with FluentValidation's `.WithErrorCode().WithMessage()`. Their pipeline runs via MediatR's `ValidationBehavior`; we're building an equivalent in-house dispatcher.

## Files

### New — Infrastructure

- `packages/api/Tsz.Infrastructure/Errors/ErrorCategory.cs` — `enum { Validation, NotFound, Conflict, Forbidden, Failure }`.
- `packages/api/Tsz.Infrastructure/Errors/ErrorCodeBase.cs` — bespoke SmartEnum-style base: `abstract class ErrorCodeBase<TEnum> { string Code; string Message; ErrorCategory Category; ... static TEnum? FromCode(string) }`. ~30 LOC, no Ardalis dep.
- `packages/api/Tsz.Infrastructure/Errors/CommonErrors.cs` — module-agnostic codes used by generic validators (e.g. `Required`, `Invalid`, `InvalidEmail`), all prefixed `ERR_`.
- `packages/api/Tsz.Infrastructure/Errors/ValidationException.cs` — thin subclass of `FluentValidation.ValidationException`. Keeps `Errors` accessible without redefining a hierarchy.
- `packages/api/Tsz.Infrastructure/Validation/FluentValidationExtensions.cs` — `RuleBuilderOptions<T,TProp>.WithError(ErrorCodeBase<TEnum>)` extension that sets `ErrorCode`, `Message`, and `CustomState = the SmartEnum instance` in one call (so the global handler reads `Category` directly without a string lookup).
- `packages/api/Tsz.Infrastructure/Cqrs/IDispatcher.cs` — `Task<TResponse> SendAsync<TResponse>(ICommand<TResponse>, CancellationToken)`. Queries left out — see Assumptions.
- `packages/api/Tsz.Infrastructure/Cqrs/Dispatcher.cs` — resolves `ICommandHandler<TCmd,TResp>` + ordered `IPipelineBehavior<TCmd,TResp>` chain from `IServiceProvider`, composes the chain, invokes.
- `packages/api/Tsz.Infrastructure/Cqrs/IPipelineBehavior.cs` — `Task<TResponse> HandleAsync(TCommand, Func<Task<TResponse>> next, CancellationToken)`.
- `packages/api/Tsz.Infrastructure/Cqrs/ValidationBehavior.cs` — resolves all `IValidator<TCommand>`, runs them, throws `ValidationException` aggregating failures; passes through on success.

### New — API

- `packages/api/Tsz.Api/Infrastructure/GlobalExceptionHandler.cs` — `IExceptionHandler`. Distinguishes `ValidationException` (expected, user-safe) from anything else (unexpected, 500 with no detail). Logs `ValidationException` at `Information`, unhandled at `Error`. Maps `ValidationException` → ProblemDetails with status from highest-severity `ErrorCategory` (priority: `NotFound` > `Conflict` > `Forbidden` > `Validation`). Body keeps `errors` keyed by property + top-level `code`.

### New — per module error catalogues

- `packages/api/Tsz.Api/Modules/Users/UserErrors.cs` — `sealed class UserErrors : ErrorCodeBase<UserErrors>`. Static instances:
  - `EmailAlreadyExists` (Conflict)
  - `NotFound` (NotFound)
- `packages/api/Tsz.Api/Modules/LeaveTypes/LeaveTypeErrors.cs`:
  - `NotFound` (NotFound)
  - `NameAlreadyExists` (Conflict)
  - `InUse` (Conflict)
- `packages/api/Tsz.Api/Modules/Users/UserLeaveErrors.cs` — shared by `AddUserLeave/UpdateUserLeave/DeleteUserLeave`:
  - `NotFound` (NotFound)
  - `UserNotFound` (NotFound)
  - `LeaveTypeNotFound` (NotFound)
  - `Duplicate` (Conflict) — same `(UserId, LeaveTypeId, Year)` already exists

### Modified — Infrastructure

- `packages/api/Tsz.Infrastructure/Extensions/ServiceCollectionExtensions.cs` — add `AddDispatcher(this IServiceCollection)` registering `IDispatcher` as scoped + reflection scan for `IPipelineBehavior<,>`. Keep existing `AddHandlersFromAssembly`.

### Modified — API

- `packages/api/Tsz.Api/Program.cs` —
  - `builder.Services.AddDispatcher()`.
  - Register `ValidationBehavior<,>` as open generic.
  - `builder.Services.AddExceptionHandler<GlobalExceptionHandler>()` + `builder.Services.AddProblemDetails()`.
  - `app.UseExceptionHandler()` BEFORE auth/endpoint middleware.

### Modified — command slices (drop custom shapes, fold business rules into validators, return plain DTOs)

Each in `packages/api/Tsz.Api/Modules/<X>/Features/<Op>.cs`:

| File | Change |
|---|---|
| `Users/Features/CreateUser.cs` | Drop `CreateUserResult`; handler returns `UserDto`. Validator adds `RuleFor(c => c.Email).MustAsync(EmailNotTaken).WithError(UserErrors.EmailAlreadyExists)`. |
| `Users/Features/UpdateUser.cs` | Drop nullable; handler returns `UserDto`. Validator: `RuleFor(c => c.Id).MustAsync(UserExists).WithError(UserErrors.NotFound)`. |
| `Users/Features/DeleteUser.cs` | Drop `bool`; handler returns `Unit` (`record struct Unit;` — see Step 1) or refactor command to not have a response. Validator: `RuleFor(c => c.Id).MustAsync(UserExists).WithError(UserErrors.NotFound)`. |
| `LeaveTypes/Features/CreateLeaveType.cs` | Drop `CreateLeaveTypeResult`; handler returns `LeaveTypeDto`. Validator: name uniqueness via `WithError(LeaveTypeErrors.NameAlreadyExists)`. |
| `LeaveTypes/Features/UpdateLeaveType.cs` | Drop nullable; handler returns `LeaveTypeDto`. Validator: `NotFound` on Id; uniqueness on rename. |
| `LeaveTypes/Features/DeleteLeaveType.cs` | Drop `DeleteLeaveTypeResult`. Validator: `NotFound` + `MustAsync(NotInUse).WithError(LeaveTypeErrors.InUse)`. |
| `Users/Features/AddUserLeave.cs` | Drop `AddUserLeaveResult`. Validator: user exists, leaveType exists, duplicate `(UserId, LeaveTypeId, Year)` check. |
| `Users/Features/UpdateUserLeave.cs` | Drop nullable; handler returns `UserLeaveDto`. Validator: existence check. |
| `Users/Features/DeleteUserLeave.cs` | Drop `bool`. Validator: existence check. |

### Modified — endpoint files

- `packages/api/Tsz.Api/Modules/Users/UserEndpoints.cs` — for each command:
  - Inject `IDispatcher` instead of the concrete handler.
  - `var dto = await dispatcher.SendAsync(cmd, ct);`
  - Remove every `.AddEndpointFilter<ValidationFilter<...>>()` line.
  - Remove every bespoke `Results.Conflict(new { error = "..." })` / `Results.NotFound()` / null-check branch — exceptions handle them.
  - Success path returns `Results.Created(...)` / `Results.Ok(dto)` / `Results.NoContent()` only.
- `packages/api/Tsz.Api/Modules/LeaveTypes/LeaveTypeEndpoints.cs` — same.
- **Queries unchanged** — they still call handlers directly, still map `null → 404` at the endpoint. No validator detour.

### Removed

- `packages/api/Tsz.Infrastructure/Validation/ValidationFilter.cs` — deleted once no slice references it (after step 6).

### Modified — Frontend

- `packages/web/src/api/client.ts`:
  ```ts
  export type ProblemDetails = {
    type?: string; title?: string; status?: number; detail?: string;
    code?: string;
    errors?: Record<string, { code: string; message: string }[]>;
  };
  export class ApiRequestError extends Error {
    constructor(public status: number, public problem: ProblemDetails | null) {
      super(`HTTP ${status}`);
    }
    get isExpected() { return this.status >= 400 && this.status < 500; }
    get userMessage() {
      return this.isExpected
        ? this.problem?.detail ?? this.problem?.title ?? 'Something went wrong.'
        : 'Something went wrong.';
    }
    get fieldErrors() { return this.problem?.errors; }
  }
  ```
- `fetch` wrapper / interceptor parses the body when `content-type` includes `application/problem+json` and feeds `ApiRequestError`.
- Form components (admin user create/edit, leavetype create/edit) — switch any inline 409 handling to consume `err.fieldErrors` for field-level highlight + `err.userMessage` for toast.
- After API stabilises: `bun --filter web gen:api`.

## Steps

Each step compiles green and tests stay green before moving on.

1. **Infra scaffold (no callers).**
   - Add `ErrorCategory`, `ErrorCodeBase<T>`, `CommonErrors`, `ValidationException`, `FluentValidationExtensions.WithError`.
   - Add `IDispatcher`, `Dispatcher`, `IPipelineBehavior`, `ValidationBehavior`.
   - Decide `Unit` shape for delete-style commands — either a `public readonly record struct Unit;` in `Tsz.Infrastructure/Cqrs/` or a non-generic `ICommand` overload. Default: add `Unit`.
   - Add unit tests covering each in isolation (see Tests).
   - `AddDispatcher()` in `ServiceCollectionExtensions` but don't call it from `Program.cs` yet.

2. **Wire dispatcher + exception handler.**
   - `Program.cs`: `AddDispatcher`, register `ValidationBehavior<,>` open-generic, `AddExceptionHandler<GlobalExceptionHandler>`, `AddProblemDetails`, `app.UseExceptionHandler()`.
   - Existing endpoints still call handlers directly — the dispatcher exists but is unused for now. Solution still compiles + integration tests still pass.

3. **Add `GlobalExceptionHandler` + tests.** Cover: single ValidationException with one category → expected status; mixed categories → highest-severity wins; unexpected exception → 500 with no detail leaked; logger called at the right level.

4. **Pilot slice: CreateUser.**
   - a. Add `UserErrors.cs` with `EmailAlreadyExists` (Conflict).
   - b. Modify `CreateUserValidator`:
     ```csharp
     RuleFor(x => x.Email)
         .MustAsync(EmailNotTaken).WithError(UserErrors.EmailAlreadyExists);
     ```
     `EmailNotTaken` injected with `IUnitOfWork`.
   - c. Simplify `CreateUserHandler`:
     - Remove `CreateUserResult`, return `Task<UserDto>`.
     - Delete the in-handler "already exists" check (validator owns it).
   - d. Modify `UserEndpoints` `POST /api/users`:
     - Inject `IDispatcher` (drop `CreateUserHandler` param).
     - `var dto = await dispatcher.SendAsync(cmd, ct); return Results.Created($"/api/users/{dto.Id}", dto);`
     - Remove `.AddEndpointFilter<ValidationFilter<CreateUserCommand>>()`.
   - e. Update unit tests: drop "returns Conflict" case (now lives in validator tests); add validator test for `EmailNotTaken`.
   - f. Update integration test: duplicate email → expect 409 ProblemDetails with `code = ERR_USER_EMAIL_ALREADY_EXISTS`.
   - g. Build + run all tests. ✓ end-to-end proof.

5. **Roll remaining command slices** — repeat (a)-(g) for `UpdateUser`, `DeleteUser`, `CreateLeaveType`, `UpdateLeaveType`, `DeleteLeaveType`, `AddUserLeave`, `UpdateUserLeave`, `DeleteUserLeave`. One commit per slice keeps the diff bounded.

6. **Remove `ValidationFilter<T>`** — grep for residual usages; delete the file + any DI registration.

7. **Frontend update.**
   - Extend `ApiRequestError` per the contract above; update `fetch` wrapper to parse `application/problem+json`.
   - `bun --filter web gen:api`.
   - Update form error consumers (`/admin/users/new`, `/admin/users/$id`, leave-type forms) to use `fieldErrors` + `userMessage`.
   - Update generic axios/fetch error toast (or wherever centralised) to surface `userMessage`.

8. **Manual smoke.**
   - Create user with duplicate email → toast shows "Email already in use" (or whatever message we set), 409 in network panel, ProblemDetails body with `code: ERR_USER_EMAIL_ALREADY_EXISTS`.
   - Update non-existent user (manual URL hack) → 404 with `code: ERR_USER_NOT_FOUND`, toast.
   - Delete LeaveType that has leaves → 409 `code: ERR_LEAVE_TYPE_IN_USE`.
   - Create user with malformed email → 400 with `errors.Email[0].code: ERR_INVALID_EMAIL`, field-level red highlight.
   - Cause a 500 (e.g. break a query in dev) → toast shows generic "Something went wrong", body is generic ProblemDetails, server log has full stack at Error level.

## Tests

### Unit (`Tsz.Api.Tests`)

- `ErrorCodeBaseTests` — `Code` is prefixed `ERR_`; `Category` exposed; `FromCode` round-trips. Inheritance test: subclass with distinct static instances.
- `DispatcherTests` — dispatcher resolves handler from DI, runs registered behaviours in order, propagates `Task<TResponse>` and exceptions. No behaviour registered → handler runs directly.
- `ValidationBehaviorTests` — no validators registered → passthrough; one passing validator → passthrough; one failing → throws `ValidationException` with the failures; multiple validators → all run, failures aggregated.
- `GlobalExceptionHandlerTests` — fake `HttpContext`:
  - `ValidationException` with single `Conflict` failure → status 409, body contains `code` + `errors`.
  - Mixed `NotFound` + `Validation` → status 404 (highest severity); body lists both.
  - Random `InvalidOperationException` → status 500, body has only generic `title`, no `detail`; logger called at Error.
  - `ValidationException` logger called at Information.
- `CreateUserValidatorTests` — add `EmailNotTaken` rule test with mocked `IUnitOfWork`.
- `CreateUserHandlerTests` — simplify: only success case + `SaveChangesAsync` called once. Drop "returns Conflict" case (moved to validator).
- Same simplification + validator tests for every migrated slice.

### Integration (`Tsz.Api.Tests.Integration`)

- `UserEndpointsTests.CreateUser_DuplicateEmail_Returns409WithCode` — seed user, POST same email → assert 409, body `Content-Type: application/problem+json`, parsed body has `code = "ERR_USER_EMAIL_ALREADY_EXISTS"`, `errors.Email[0].code` matches.
- `UserEndpointsTests.UpdateUser_UnknownId_Returns404WithCode`.
- `UserEndpointsTests.DeleteUser_UnknownId_Returns404`.
- `UserEndpointsTests.CreateUser_InvalidEmail_Returns400WithFieldErrors` — body has `errors.Email[0].code = ERR_INVALID_EMAIL`.
- `LeaveTypeEndpointsTests.DeleteLeaveType_InUse_Returns409WithCode`.
- `LeaveTypeEndpointsTests.DeleteLeaveType_UnknownId_Returns404`.
- `UserLeaveEndpointsTests.AddUserLeave_DuplicateYear_Returns409`.
- Add **one** test-only endpoint inside `Tsz.Api.Tests.Integration` that throws a random exception; hit it; assert 500 + generic title + no exception message in the body.

## Edge Cases

- **Mixed-category failures in one request.** `ValidationException` carries N failures of different categories. Pick highest severity for status: `NotFound > Conflict > Forbidden > Validation`. Body lists every failure.
- **TOCTOU race on uniqueness.** Validator's `EmailNotTaken` passes; concurrent request inserts first; handler hits `DbUpdateException` from the unique index. That's treated as unexpected → 500. Acceptable: the DB index is the source of truth, validator is the UX-friendly pre-check. Worth documenting alongside `CreateUser`.
- **Validators doing DB work.** Every `MustAsync(...)` adds a query. At tsz scale (small admin tool) this is fine; if a heavy slice ever materialises, batch the lookups in a custom rule.
- **Internal callers (handler-to-handler).** None today (`grep HandleAsync packages/api/Tsz.Api/Modules` confirms). If ever introduced, those calls bypass the dispatcher and skip validation. Document the constraint on `IDispatcher`. Re-evaluate when first needed.
- **Empty `ValidationException.Errors`.** Shouldn't happen, but guard: treat as plain 400 with generic title.
- **Query-side errors.** Queries don't use the dispatcher; not-found still maps `null → 404` at the endpoint. Frontend's `ApiRequestError` parser still handles bare 404 (with or without a ProblemDetails body).
- **ProblemDetails key collisions.** Stable extension keys: `code` (top-level error code, optional), `errors` (per-property failure map). Never collide with built-in `type/title/status/detail/instance`.
- **Frontend migration window.** `ApiRequestError.problem` is nullable — legacy endpoints (during the slice-by-slice rollout) still return non-ProblemDetails bodies; the FE handles both shapes.
- **5xx leak prevention.** `GlobalExceptionHandler` builds the 500 body itself; do **not** rely on ASP.NET's default `IncludeExceptionDetails` flag.

## Assumptions

- Sticking with the in-house `ICommandHandler`/`IQueryHandler` interfaces. No MediatR.
- Bespoke `ErrorCodeBase<T>` (~30 LOC) — no `Ardalis.SmartEnum` dependency. If the user later prefers Ardalis, swap is ~5 LOC + a package reference.
- `IUnitOfWork` is request-scoped (confirmed via `EfCoreUnitOfWork` registration in `ServiceCollectionExtensions`). Dispatcher and handler get the same instance.
- Frontend can change atomically with the API in one branch (single dev, no external consumers).
- Queries skip the dispatcher entirely. Symmetry argument for adding `IDispatcher.QueryAsync<>` later is weak — queries have no behaviours to run. Defer.
- Highest-severity status mapping order is `NotFound > Conflict > Forbidden > Validation`. If product wants a different priority (e.g. surface field-validation first), flip in the handler — one comparator change.
- `ValidationException` extends FluentValidation's existing exception type, not a new hierarchy. Keeps wire/test surface familiar.
- The dispatcher's behaviour ordering is intentional: `ValidationBehavior` first, handler last. No transaction behaviour, no logging behaviour — add later if and when needed.
- Manual smoke at step 8 is sufficient validation; we're not gating on automated browser tests for this refactor.
