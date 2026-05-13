---
name: 'backend-slice'
description: >
  Add one operation (or a CRUD bundle) to an existing module in packages/api/Tsz.Api.
  Each slice gets its own file with command/query, response, handler implementing
  ICommandHandler / IQueryHandler, and FluentValidation validator for writes.
  Endpoint is registered in the module's endpoints file. Use after backend-module.
paths: packages/api/**
---

# Backend Slice

Adds a vertical slice to an existing module. One file per operation, one endpoint registration. No mediator — endpoint resolves the handler interface from DI directly.

## Conventions
- Slice file: `Modules/<Feature>/<Operation><Feature>.cs` — e.g. `CreateAnimal.cs`, `GetAnimals.cs`
- Each file contains: command/query record, validator (writes only), handler
- Response shape lives **in the slice** unless ≥2 slices share the *exact* same shape — only then promote to a module-level DTO
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

For a CRUD bundle, repeat Steps 2–4 for each operation.

## Step 2 — Slice file

**Command example — `CreateAnimal.cs`:**

```csharp
using Tsz.Infrastructure.Abstractions;
using FluentValidation;

namespace Tsz.Api.Modules.Animals;

public sealed record CreateAnimalCommand(string Name, string Species, int Age)
    : ICommand<AnimalDto>;

public sealed class CreateAnimalValidator : AbstractValidator<CreateAnimalCommand>
{
    public CreateAnimalValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Species).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Age).InclusiveBetween(0, 200);
    }
}

public sealed class CreateAnimalHandler(IUnitOfWork uow)
    : ICommandHandler<CreateAnimalCommand, AnimalDto>
{
    public async Task<AnimalDto> HandleAsync(CreateAnimalCommand command, CancellationToken ct = default)
    {
        var animal = Animal.Create(command.Name, command.Species, command.Age);
        uow.RepositoryFor<Animal>().Add(animal);
        await uow.SaveChangesAsync(ct);
        return AnimalDto.ToDto(animal);
    }
}
```

**Query example — `GetAnimalById.cs`:**

```csharp
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Modules.Animals;

public sealed record GetAnimalByIdQuery(Guid Id) : IQuery<AnimalDto?>;

public sealed class GetAnimalByIdHandler(IUnitOfWork uow)
    : IQueryHandler<GetAnimalByIdQuery, AnimalDto?>
{
    public Task<AnimalDto?> HandleAsync(GetAnimalByIdQuery query, CancellationToken ct = default) =>
        uow.RepositoryFor<Animal>()
            .FirstOrDefaultAsDtoAsync<AnimalDto>(a => a.Id == query.Id, ct);
}
```

**Shared module-level DTO (`AnimalDto.cs`)** — opts in to EF projection:

```csharp
using System.Linq.Expressions;
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Modules.Animals;

public sealed record AnimalDto(Guid Id, string Name, string Species, int Age)
    : IEntityDto<Animal, AnimalDto>
{
    public static Expression<Func<Animal, AnimalDto>> Project =>
        a => new AnimalDto(a.Id, a.Name, a.Species, a.Age);

    public static AnimalDto ToDto(Animal entity) =>
        new(entity.Id, entity.Name, entity.Species, entity.Age);
}
```

**Update commands** load via `repo.GetByIdAsync` then call named mutators:
```csharp
var animal = await uow.RepositoryFor<Animal>().GetByIdAsync(command.Id, ct);
if (animal is null) return null;
animal.Rename(command.Name);
await uow.SaveChangesAsync(ct);
```

## Step 3 — Register endpoint

In `<Feature>Endpoints.cs` inside `Map()`, depend on the handler **interface** (not the concrete type):

```csharp
// GET list
group.MapGet("/", async (
    IQueryHandler<GetAnimalsQuery, IReadOnlyList<AnimalDto>> handler,
    CancellationToken ct) =>
        TypedResults.Ok(await handler.HandleAsync(new GetAnimalsQuery(), ct)));

// GET by id
group.MapGet("/{id:guid}", async (
    Guid id,
    IQueryHandler<GetAnimalByIdQuery, AnimalDto?> handler,
    CancellationToken ct) =>
{
    var animal = await handler.HandleAsync(new GetAnimalByIdQuery(id), ct);
    return animal is not null ? Results.Ok(animal) : Results.NotFound();
});

// POST
group.MapPost("/", async (
    CreateAnimalCommand command,
    ICommandHandler<CreateAnimalCommand, AnimalDto> handler,
    CancellationToken ct) =>
{
    var animal = await handler.HandleAsync(command, ct);
    return Results.Created($"/api/animals/{animal.Id}", animal);
}).AddEndpointFilter<ValidationFilter<CreateAnimalCommand>>();
```

## Step 4 — If the entity changed, regenerate the migration

If this slice added a new field, index, or constraint to the entity / `<Feature>Configuration.cs`, generate an EF migration:

```
dotnet ef migrations add <DescriptiveName> \
  --project packages/api/Tsz.Api \
  --startup-project packages/api/Tsz.Api \
  --output-dir Migrations
```

Commit the migration + `AnimalDbContextModelSnapshot.cs` along with the slice. See `ef-migration` for hand-edit guidance (data backfills, SQLite ALTER limits).

Pure read slices and slices that only add commands/queries over an unchanged schema don't need a migration.

## Step 5 — Verify

`bun run check`. Then add tests with `backend-unit-test` (handler + validator in isolation) and/or `backend-integration-test` (HTTP).
