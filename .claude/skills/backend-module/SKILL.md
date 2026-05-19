---
name: 'backend-module'
description: >
  Scaffold a new domain module in packages/api/Tsz.Api: DDD-ish entity (private setters,
  static Create factory, named mutators), EF IEntityTypeConfiguration (auto-discovered),
  endpoint file shell with Map(), and Program.cs wiring. No DbSet plumbing — all access
  flows through IUnitOfWork / IRepository. Use once per new domain concept before adding
  slices with backend-slice.
paths: packages/api/**
---

# Backend Module

One-time scaffold for a new domain module. Run this first, then use `backend-slice` to add operations. Users is the canonical live example to mirror.

## Conventions
- Module lives in `packages/api/Tsz.Api/Modules/<Feature>/`
- Slice files (commands/queries/handlers) live in `Modules/<Feature>/Features/`
- Entity implements `IEntityBase` (`Guid Id`); writable state has **private setters**; construction via `static Create(...)`; mutation via named methods (`Rename`, `ChangeRole`, …)
- EF config in `<Feature>Configuration.cs` implementing `IEntityTypeConfiguration<T>` — picked up automatically by `AppDbContext.OnModelCreating` via `ApplyConfigurationsFromAssembly`. Nothing else needs to be done to register the entity — `AppDbContext` has no explicit `DbSet<T>` properties; everything flows through `context.Set<T>()` inside the repo.
- One shared `AppDbContext` at `Tsz.Api/Persistence/AppDbContext.cs` for the whole API — never spin up a per-module DbContext
- All persistence access from slices, seeders, and tests goes through `IUnitOfWork` → `IRepository<T>` (in `Tsz.Infrastructure.Abstractions`). Direct `AppDbContext` access is reserved for `Program.cs` startup wiring (`Migrate`/`EnsureCreated`).
- Endpoints file: `<Feature>Endpoints.cs` — one static class, one `Map()` method
- Register `Map()` in `Program.cs` next to `UserEndpoints.Map(app)`
- After changes run `bun run check` (or `dotnet build packages/api/Tsz.Api`) and fix all errors

## Step 1 — Clarify scope
- Feature name (PascalCase, used for folder and class names)
- What fields does the entity have, and which are mutable after creation?
- Are any cross-entity relationships needed (FKs to other modules)?

### Cross-module relationships

Modules reference each other by **`Guid` FK only** — no EF navigation properties and no DB-level foreign-key constraints between tables that live in different modules. This keeps modules independently evolvable and avoids EF eagerly pulling foreign aggregates through `Include`s.

- Carry the related id as a plain `Guid` (or `Guid?`) property on the entity.
- In the `IEntityTypeConfiguration`, map it as `builder.Property(x => x.OtherId)` — **no** `HasOne(...).WithMany(...)`, **no** `HasForeignKey(...)`.
- Cross-module integrity (the referenced row exists, is in the right state, etc.) is enforced in the slice's **FluentValidation validator** via `MustAsync` + the other module's `IRepository<T>`, not by the database.
- The convention only applies across modules. Inside a single module, owned value objects (`OwnsOne` / `OwnsMany`) and parent/child entity collections are fine — see `User.RoleAssignments` for a same-module owned-collection example.

Example — `Customer.ClientManagerId` references a `User` from another module:

```csharp
// Modules/Customers/Customer.cs
public Guid? ClientManagerId { get; private set; }
public void AssignClientManager(Guid? userId) => ClientManagerId = userId;

// Modules/Customers/CustomerConfiguration.cs
builder.Property(c => c.ClientManagerId); // plain column, no nav, no FK

// Modules/Customers/Features/UpdateCustomer.cs (validator)
RuleFor(x => x.ClientManagerId!.Value)
    .MustAsync(UserExists).WithError(CustomerErrors.ClientManagerNotFound)
    .When(x => x.ClientManagerId is not null);
```

## Step 2 — Entity

`Modules/<Feature>/<Feature>.cs`:
```csharp
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Modules.<Feature>;

public class <Feature> : IEntityBase
{
    public Guid Id { get; set; }
    public string Name { get; private set; } = string.Empty;
    // additional properties with private setters

    private <Feature>() { } // EF

    public static <Feature> Create(string name /*, ... */) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
    };

    public void Rename(string name) => Name = name;
    // additional named mutators per allowed state transition
}
```

**Do not** expose public setters — handlers must mutate via named methods so invariants live on the entity.

## Step 3 — EF configuration

`Modules/<Feature>/<Feature>Configuration.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Tsz.Api.Modules.<Feature>;

public class <Feature>Configuration : IEntityTypeConfiguration<<Feature>>
{
    public void Configure(EntityTypeBuilder<<Feature>> builder)
    {
        builder.ToTable("<Feature>s");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        // additional constraints, indexes
    }
}
```

No registration needed — `AppDbContext` calls `ApplyConfigurationsFromAssembly` and picks it up.

## Step 4 — DbContext registration

Nothing to do. `AppDbContext.OnModelCreating` calls `ApplyConfigurationsFromAssembly`, which discovers your new `<Feature>Configuration` and registers the entity. No `DbSet<T>` property is needed — slices, seeders, and tests reach the entity via `IUnitOfWork.RepositoryFor<<Feature>>()`, which internally calls `context.Set<<Feature>>()`.

## Step 5 — Endpoint file shell

`Modules/<Feature>/<Feature>Endpoints.cs`:
```csharp
using Tsz.Infrastructure.Endpoints;

namespace Tsz.Api.Modules.<Feature>;

public static class <Feature>Endpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapApiGroup("<feature>s");

        // slices go here — add with backend-slice
    }
}
```

`MapApiGroup` lives in `Tsz.Infrastructure.Endpoints` and prefixes routes with `/api/`.

## Step 6 — Wire up in Program.cs

At the end of the pipeline, next to the existing module wires:

```csharp
<Feature>Endpoints.Map(app);
```

`AddDbContext<AppDbContext>(...)`, `AddInfrastructure<AppDbContext>()`, `AddHandlersFromAssembly(...)`, and `AddValidatorsFromAssembly(...)` are already wired once at the top of `Program.cs`. Handlers and validators in the new module are discovered automatically — no per-handler registration.

## Step 7 — Test builder

Every entity gets a builder in `packages/api/Tsz.Api.Tests/Builders/<Feature>Builder.cs`. The builder calls the static `Create(...)` factory (so invariants stay enforced) and exposes one `WithXxx` extension per named mutator.

```csharp
using FizzWare.NBuilder.Generators;
using Tsz.Api.Modules.<Feature>;

namespace Tsz.Api.Tests.Builders;

public static class <Feature>Builder
{
    public static <Feature> Build()
    {
        var id = Guid.NewGuid();
        var entity = <Feature>.Create(
            name: "Name_" + id.ToString()[..8]
            /* ... fill remaining required Create params */);
        entity.Id = id;
        return entity;
    }

    public static <Feature> WithId(this <Feature> entity, Guid id)
    {
        entity.Id = id;
        return entity;
    }

    public static <Feature> WithName(this <Feature> entity, string name)
    {
        entity.Rename(name);
        return entity;
    }

    // one WithXxx per named mutator on the entity
}
```

`UserBuilder.cs` is the canonical example. Use `GetRandom.Int(min, max)` / `GetRandom.Email()` / `GetRandom.AlphaString(n)` for primitives. Builders ship sensible defaults so test call sites only spell out the field they care about: `UserBuilder.Build().WithRole(UserRole.Admin)`.

## Step 8 — Generate the migration

The new entity needs a schema. From the repo root:

```
dotnet ef migrations add Add<Feature> \
  --project packages/api/Tsz.Api \
  --startup-project packages/api/Tsz.Api \
  --output-dir Persistence/Migrations
```

Inspect the generated `Up()` to confirm the `CreateTable` looks right. Commit the migration + updated `AppDbContextModelSnapshot.cs`. Migrations run automatically on next API start via `db.Database.Migrate()` in `Program.cs`. See the `ef-migration` skill for hand-edit guidance (data backfills, SQLite ALTER limits).

Note: `dotnet ef` discovers the entity via the configuration's `ApplyConfigurationsFromAssembly` call, not via a `DbSet<T>` property. No `AppDbContext` edit is required.

## Step 9 — Verify

Run `bun run check`. Then add operations with `backend-slice`.
