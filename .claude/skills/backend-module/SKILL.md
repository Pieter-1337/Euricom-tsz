---
name: 'backend-module'
description: >
  Scaffold a new domain module in packages/api/Tsz.Api: DDD-ish entity (private setters,
  static Create factory, named mutators), EF IEntityTypeConfiguration, DbSet registration,
  endpoint file shell with Map(), and Program.cs wiring.
  Use once per new domain concept before adding slices with backend-slice.
---

# Backend Module

One-time scaffold for a new domain module. Run this first, then use `backend-slice` to add operations.

## Conventions
- Module lives in `packages/api/Tsz.Api/Modules/<Feature>/`
- Entity implements `IEntityBase` (`Guid Id`); all writable state has **private setters**; construction via `static Create(...)`; mutation via named methods (`Rename`, `ChangeAge`, ...)
- EF config in `<Feature>Configuration.cs` implementing `IEntityTypeConfiguration<T>`
- Endpoints file: `<Feature>Endpoints.cs` — one static class, one `Map()` method
- Register endpoint `Map()` in `Program.cs` next to `AnimalEndpoints.Map(app)`
- After changes run `bun run build:api` and fix all errors

## Step 1 — Clarify scope
- Feature name (PascalCase, used for folder and class names)
- What fields does the entity have, and which are mutable after creation?
- Does this module share an existing `DbContext` or need a new one?

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

## Step 4 — DbContext

If reusing an existing context, add to it. Otherwise create `<Feature>DbContext.cs`:
```csharp
using Microsoft.EntityFrameworkCore;

namespace Tsz.Api.Modules.<Feature>;

public class <Feature>DbContext(DbContextOptions<<Feature>DbContext> options) : DbContext(options)
{
    public DbSet<<Feature>> <Feature>s => Set<<Feature>>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfiguration(new <Feature>Configuration());
}
```

## Step 5 — Endpoint file shell

`Modules/<Feature>/<Feature>Endpoints.cs`:
```csharp
using Tsz.Api.Common.Extensions;

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

## Step 6 — Wire up in Program.cs

Module-specific:
```csharp
builder.Services.AddDbContext<<Feature>DbContext>(options => options.UseSqlite(/* ... */));
builder.Services.AddInfrastructure<<Feature>DbContext>(); // only if new DbContext

// At end of pipeline:
<Feature>Endpoints.Map(app);
```

Handlers and validators are picked up automatically by `AddHandlersFromAssembly` and `AddValidatorsFromAssembly` already wired in `Program.cs` — no per-handler registration needed.

## Step 7 — Generate the migration

The new entity needs a schema. From the repo root:

```
dotnet ef migrations add Add<Feature> \
  --project packages/api/Tsz.Api \
  --startup-project packages/api/Tsz.Api \
  --output-dir Migrations
```

Inspect the generated `Up()` to confirm the `CreateTable` looks right. Commit the migration + updated `AnimalDbContextModelSnapshot.cs`. The migration runs automatically on next API start via `db.Database.Migrate()` in `Program.cs`. See the `ef-migration` skill for more.

If this module needs its own DbContext (Step 4 path), the `--context` flag is required:

```
dotnet ef migrations add Add<Feature> \
  --project packages/api/Tsz.Api \
  --startup-project packages/api/Tsz.Api \
  --context <Feature>DbContext \
  --output-dir Migrations/<Feature>
```

And `Program.cs` needs a `Migrate()` call against the new context too.

## Step 8 — Verify

Run `bun run check`. Then add operations with `backend-slice`.
