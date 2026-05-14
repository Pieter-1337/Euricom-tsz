---
name: 'backend-slice'
description: >
  Add one operation (or a CRUD bundle) to an existing module in packages/api/Tsz.Api.
  Each slice gets its own file under Modules/<Feature>/Features/ with command/query,
  response, handler implementing ICommandHandler / IQueryHandler, and FluentValidation
  validator for writes. Endpoint is registered in the module's endpoints file. Use
  after backend-module.
paths: packages/api/**
---

# Backend Slice

Adds a vertical slice to an existing module. One file per operation, one endpoint registration. Commands flow through `IDispatcher` (CQRS pipeline with validation); queries call handlers directly.

## Conventions
- Slice file: `Modules/<Feature>/Features/<Operation><Feature>.cs` — e.g. `CreateUser.cs`, `GetUserById.cs`
- Each file contains: command/query record, validator (writes only), handler — all in `Tsz.Api.Modules.<Feature>.Features`
- Response shape lives **in the slice** unless ≥2 slices share the *exact* same shape — only then promote to a module-level DTO (e.g. `UserDto.cs` at the module root)
- Commands implement `ICommand<TResponse>`; handler implements `ICommandHandler<TCommand, TResponse>`
- Queries implement `IQuery<TResponse>`; handler implements `IQueryHandler<TQuery, TResponse>`
- All handler methods are named `HandleAsync` and take a `CancellationToken`
- Atomicity comes from a single `IUnitOfWork.SaveChangesAsync(ct)` call per handler — EF wraps it in an implicit transaction.
- Writes use `Entity.Create(...)` / named mutators on the entity — never property-bag construction
- Reads use `repo.GetAllAsDtosAsync<TDto>` / `repo.FirstOrDefaultAsDtoAsync<TDto>` with a DTO that implements `IEntityDto<TEntity, TDto>`
- **Validation via FluentValidation**: Write an `AbstractValidator<TCommand>` injecting `IUnitOfWork` for async checks. Use `.WithError(ErrorCodeBase)` to attach business-rule error codes with categories. Validators auto-register via `AddValidatorsFromAssembly`; the dispatcher pipeline runs them automatically — no manual endpoint filters.
- Handlers and validators auto-register via `AddHandlersFromAssembly` / `AddValidatorsFromAssembly` — never wire them by hand
- **Handler responses are plain DTOs** (or `Tsz.Infrastructure.Cqrs.Unit` for deletes). No custom result records or bool/nullable returns — errors flow as `ValidationException`.
- After changes run `bun run build:api` and fix all errors

## Step 1 — Clarify scope

Ask:
- Single operation or full CRUD bundle?
- Query (GET, returns data) or command (POST/PUT/DELETE, mutates)?
- What does the request look like? What does it return?
- Is the response identical to an existing module-level DTO, or slice-private?

For a CRUD bundle, repeat Steps 2–3 for each operation.

## Step 2 — Slice file

**Command example — `Features/CreateUser.cs`:**

```csharp
using FluentValidation;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Validation;

namespace Tsz.Api.Modules.Users.Features;

public sealed record CreateUserCommand(string Name, string Email, UserRole Role)
    : ICommand<UserDto>;

public sealed class CreateUserValidator : AbstractValidator<CreateUserCommand>
{
    private readonly IUnitOfWork _uow;

    public CreateUserValidator(IUnitOfWork uow)
    {
        _uow = uow;

        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Role).IsInEnum();
        RuleFor(x => x.Email)
            .MustAsync(EmailNotTaken).WithError(UserErrors.EmailAlreadyExists)
            .When(x => !string.IsNullOrEmpty(x.Email));
    }

    private async Task<bool> EmailNotTaken(string email, CancellationToken ct)
    {
        var exists = await _uow.RepositoryFor<User>()
            .ExistsAsync(u => u.Email == email, ct);
        return !exists;
    }
}

public sealed class CreateUserHandler(IUnitOfWork uow)
    : ICommandHandler<CreateUserCommand, UserDto>
{
    public async Task<UserDto> HandleAsync(CreateUserCommand command, CancellationToken ct = default)
    {
        var user = User.Create(command.Name, command.Email, command.Role);
        uow.RepositoryFor<User>().Add(user);
        await uow.SaveChangesAsync(ct);
        return UserDto.ToDto(user);
    }
}
```

**Query example — `Features/GetUserById.cs`:**

```csharp
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Modules.Users.Features;

public sealed record GetUserByIdQuery(Guid Id) : IQuery<UserDto?>;

public sealed class GetUserByIdHandler(IUnitOfWork uow)
    : IQueryHandler<GetUserByIdQuery, UserDto?>
{
    public Task<UserDto?> HandleAsync(GetUserByIdQuery query, CancellationToken ct = default) =>
        uow.RepositoryFor<User>()
            .FirstOrDefaultAsDtoAsync<UserDto>(u => u.Id == query.Id, ct);
}
```

**Shared module-level DTO (`UserDto.cs` at the module root)** — opts in to EF projection:

```csharp
using System.Linq.Expressions;
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Modules.Users;

public sealed record UserDto(Guid Id, string Email, string Name, UserRole Role)
    : IEntityDto<User, UserDto>
{
    public static Expression<Func<User, UserDto>> Project =>
        u => new UserDto(u.Id, u.Email, u.Name, u.Role);

    public static UserDto ToDto(User entity) =>
        new(entity.Id, entity.Email, entity.Name, entity.Role);
}
```

**Update commands** load via `repo.GetByIdAsync` then call named mutators:
```csharp
var user = await uow.RepositoryFor<User>().GetByIdAsync(command.Id, ct);
if (user is null) return null;
user.Rename(command.Name);
await uow.SaveChangesAsync(ct);
```

## Step 3 — Register endpoint

In `<Feature>Endpoints.cs` inside `Map()`, inject the handler/dispatcher interface and call it directly. Validation errors automatically throw `ValidationException`, caught by the global handler.

```csharp
// GET list
group.MapGet("/", async (
    IQueryHandler<GetUsersQuery, IReadOnlyList<UserDto>> handler,
    CancellationToken ct) =>
        TypedResults.Ok(await handler.HandleAsync(new GetUsersQuery(), ct)));

// GET by id
group.MapGet("/{id:guid}", async (
    Guid id,
    IQueryHandler<GetUserByIdQuery, UserDto?> handler,
    CancellationToken ct) =>
{
    var user = await handler.HandleAsync(new GetUserByIdQuery(id), ct);
    return user is not null ? Results.Ok(user) : Results.NotFound();
});

// POST — use IDispatcher for commands
group.MapPost("/", async (
    CreateUserCommand command,
    IDispatcher dispatcher,
    CancellationToken ct) =>
{
    var dto = await dispatcher.SendAsync(command, ct);
    return Results.CreatedAtRoute("GetUserById", new { id = dto.Id }, dto);
});

// PUT — dispatcher handles validation + error mapping
group.MapPut("/{id:guid}", async (
    Guid id,
    UpdateUserCommand command,
    IDispatcher dispatcher,
    CancellationToken ct) =>
{
    if (command.Id != id)
        return Results.BadRequest("Route id does not match command id.");

    var dto = await dispatcher.SendAsync(command, ct);
    return Results.Ok(dto);
});

// DELETE — returns Unit from dispatcher
group.MapDelete("/{id:guid}", async (
    Guid id,
    IDispatcher dispatcher,
    CancellationToken ct) =>
{
    await dispatcher.SendAsync(new DeleteUserCommand(id), ct);
    return Results.NoContent();
});
```

For routes that need authorization, layer a sub-group: `group.MapGroup("").RequireAuthorization(AuthorizationPolicies.RequireAdmin)` — see `UserEndpoints.cs` for the live pattern.

## Step 4 — If the entity changed, regenerate the migration

If this slice added a new field, index, or constraint to the entity / `<Feature>Configuration.cs`, generate an EF migration:

```
dotnet ef migrations add <DescriptiveName> \
  --project packages/api/Tsz.Api \
  --startup-project packages/api/Tsz.Api \
  --output-dir Persistence/Migrations
```

Commit the migration + `AppDbContextModelSnapshot.cs` along with the slice. See `ef-migration` for hand-edit guidance (data backfills, SQLite ALTER limits).

Pure read slices and slices that only add commands/queries over an unchanged schema don't need a migration.

## Step 5 — Verify

`bun run check`. Then add tests with `backend-unit-test` (handler + validator in isolation) and/or `backend-integration-test` (HTTP).
