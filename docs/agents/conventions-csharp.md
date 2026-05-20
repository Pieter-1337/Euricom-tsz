# C# Guidelines

## Command/Query Validation & Error Handling

- **Validators as business rules**: Write `AbstractValidator<TCommand>` subclasses in `FluentValidation`. Use `.MustAsync(...)` for async checks (e.g., `EmailNotTaken`). Attach error codes via `.WithError(ErrorCodeBase)` to embed category metadata.
- **Error codes**: Create a `sealed class XxxErrors : ErrorCodeBase<XxxErrors>` per module with static instances. Example: `UserErrors.EmailAlreadyExists` (category `Conflict`), `UserErrors.NotFound` (category `NotFound`). Codes auto-prefix `ERR_` and map to HTTP status via the global handler.
- **Handler responses**: Commands return plain DTOs (or `Tsz.Infrastructure.Cqrs.Unit` for delete operations). No custom result records, no nullables for "not found" — those errors flow through validation/global handler as `ValidationException`.

## Endpoints & Dispatcher

- **Commands**: Inject `IDispatcher`, call `await dispatcher.SendAsync(command, ct)`. Returns the DTO directly.
- **Queries**: Inject `IQueryHandler<TQuery, TResponse>` directly, call `handler.HandleAsync(query, ct)`.
- **Response mapping**: Translate handler responses using `Results.Created(...)`, `Results.Ok(dto)`, `Results.NoContent()`. No `.AddEndpointFilter<ValidationFilter<T>>()` — validation runs in the dispatcher pipeline.

## Soft-delete

- Aggregate entities that support soft-delete implement `ISoftDeletable` (extends `IEntityBase`) with `DateTimeOffset? DeletedAt`. The entity exposes a `SoftDelete(DateTimeOffset at)` mutator and the EF configuration adds `HasQueryFilter(e => e.DeletedAt == null)`.
- Paged queries on soft-deletable entities call the constrained `repo.GetPagedAsync(opts, sortMap, searchable, projection, ct)` extension (in `Tsz.Infrastructure.Common.Pagination`). It reads `opts.DeletedOnly`, applies the soft-delete predicate, and toggles `ignoreQueryFilters` automatically.
- Non-soft-deletable paging falls back to the underlying `IRepository.GetPagedAsync(...)` instance method, which accepts an explicit `filter` and `ignoreQueryFilters`.

## General

- After C# changes, run `bun run build:api` and fix all errors