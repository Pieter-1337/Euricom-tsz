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

Adds a vertical slice to an existing module. One file per operation, one endpoint registration. No mediator — endpoint resolves the handler interface from DI directly.

## Conventions
- Slice file: `Modules/<Feature>/Features/<Operation><Feature>.cs` — e.g. `CreateUser.cs`, `GetUserById.cs`
- Each file contains: command/query record, validator (writes only), handler — all in `Tsz.Api.Modules.<Feature>.Features`
- Response shape lives **in the slice** unless ≥2 slices share the *exact* same shape — only then promote to a module-level DTO (e.g. `UserDto.cs` at the module root)
- Commands implement `ICommand<TResponse>`; handler implements `ICommandHandler<TCommand, TResponse>`
- Queries implement `IQuery<TResponse>`; handler implements `IQueryHandler<TQuery, TResponse>`
- All handler methods are named `HandleAsync` and take a `CancellationToken`
- Transactions live **inside the handler** via `IUnitOfWork.SaveChangesAsync(ct)` for single-step writes; explicit `Begin/CloseTransactionAsync` only for multi-step
- Writes use `Entity.Create(...)` / named mutators on the entity — never property-bag construction
- Reads use `repo.GetAllAsDtosAsync<TDto>` / `repo.FirstOrDefaultAsDtoAsync<TDto>` with a DTO that implements `IEntityDto<TEntity, TDto>`
- Validation via FluentValidation, applied through `.AddEndpointFilter<ValidationFilter<TCommand>>()`
- Handlers and validators auto-register via `AddHandlersFromAssembly` / `AddValidatorsFromAssembly` — never wire them by hand
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
using Tsz.Infrastructure.Abstractions;
using FluentValidation;

namespace Tsz.Api.Modules.Users.Features;

public sealed record CreateUserCommand(string Name, string Email, UserRole Role)
    : ICommand<CreateUserResult>;

public sealed record CreateUserResult(UserDto? User, bool Conflict)
{
    public static CreateUserResult Created(UserDto user) => new(user, false);
    public static CreateUserResult EmailConflict() => new(null, true);
}

public sealed class CreateUserValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Role).IsInEnum();
    }
}

public sealed class CreateUserHandler(IUnitOfWork uow)
    : ICommandHandler<CreateUserCommand, CreateUserResult>
{
    public async Task<CreateUserResult> HandleAsync(CreateUserCommand command, CancellationToken ct = default)
    {
        var repo = uow.RepositoryFor<User>();
        if (await repo.ExistsAsync(u => u.Email == command.Email, ct))
            return CreateUserResult.EmailConflict();

        var user = User.Create(command.Name, command.Email, command.Role);
        repo.Add(user);
        await uow.SaveChangesAsync(ct);
        return CreateUserResult.Created(UserDto.ToDto(user));
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

In `<Feature>Endpoints.cs` inside `Map()`, depend on the handler **interface** (not the concrete type):

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

// POST
group.MapPost("/", async (
    CreateUserCommand command,
    ICommandHandler<CreateUserCommand, CreateUserResult> handler,
    CancellationToken ct) =>
{
    var result = await handler.HandleAsync(command, ct);
    return result.Conflict
        ? Results.Conflict(new { error = "A user with this email already exists." })
        : Results.Created($"/api/users/{result.User!.Id}", result.User);
}).AddEndpointFilter<ValidationFilter<CreateUserCommand>>();
```

`ValidationFilter<T>` lives in `Tsz.Infrastructure.Validation`. For routes that need authorization, layer a sub-group: `group.MapGroup("").RequireAuthorization(AuthorizationPolicies.RequireAdmin)` — see `UserEndpoints.cs` for the live pattern.

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
