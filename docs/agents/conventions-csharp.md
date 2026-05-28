# C# Guidelines

## Command/Query Validation & Error Handling

- **Validators as business rules**: Write `AbstractValidator<TCommand>` subclasses in `FluentValidation`. Use `.MustAsync(...)` for async checks (e.g., `EmailNotTaken`). Attach error codes via `.WithError(ErrorCodeBase)` to embed category metadata.
- **Error codes**: Create a `sealed class XxxErrors : ErrorCodeBase<XxxErrors>` per module with static instances. Example: `UserErrors.EmailAlreadyExists` (category `Conflict`), `UserErrors.NotFound` (category `NotFound`). Codes auto-prefix `ERR_` and map to HTTP status via the global handler.
- **Handler responses**: Commands return plain DTOs (or `Tsz.Infrastructure.Cqrs.Unit` for delete operations). No custom result records, no nullables for "not found" — those errors flow through validation/global handler as `ValidationException`.

## Endpoints & Dispatcher

- **Commands**: Inject `IDispatcher`, call `await dispatcher.SendAsync(command, ct)`. Returns the DTO directly.
- **Queries**: Inject `IQueryHandler<TQuery, TResponse>` directly, call `handler.HandleAsync(query, ct)`.
- **Response mapping**: Always use `TypedResults.*` (not `Results.*`) — e.g. `TypedResults.Ok(dto)`, `TypedResults.CreatedAtRoute(dto, "RouteName", new { id = dto.Id })`, `TypedResults.NoContent()`. Annotate the handler return type with the concrete `Ok<TDto>` / `CreatedAtRoute<TDto>` / `NoContent` (or `Results<…>` union for multi-status endpoints, e.g. `Task<Results<Ok<UserDto>, NotFound>>`). The untyped `Results.*` helpers return `IResult` and the OpenAPI pipeline can't infer the DTO — `bun --filter web gen:api` will silently drop it from `schema.ts`. No `.AddEndpointFilter<ValidationFilter<T>>()` — validation runs in the dispatcher pipeline.

## Soft-delete

- Every entity is soft-deletable by convention: `IEntityBase` extends `ISoftDeletable` (`DateTimeOffset? DeletedAt`). Aggregates that actually use soft-delete add a `SoftDelete(DateTimeOffset at)` mutator and a **named** EF query filter: `builder.HasQueryFilter("SoftDelete", e => e.DeletedAt == null)`. The name `"SoftDelete"` is the project-wide convention. Entities that don't soft-delete (e.g., child rows like `UserLeave`, reference data like `LeaveType`) declare the property but call `builder.Ignore(e => e.DeletedAt)` so EF doesn't try to map a column.
- Paged queries call `repo.GetPagedAsync(opts, sortMap, searchable, projection, filter, ct, ignoreQueryFilters)`. The repo reads `opts.DeletedOnly` and internally bypasses only the `"SoftDelete"` filter via `IgnoreQueryFilters(new[] { "SoftDelete" })`, then applies `Where(e => e.DeletedAt != null)`. The caller's `ignoreQueryFilters` flag is independent — it bypasses *all* global filters (escape hatch for genuinely unrelated reads). Optional `filter` is AND-composed on top.

## General

- After C# changes, run `bun run build:api` and fix all errors