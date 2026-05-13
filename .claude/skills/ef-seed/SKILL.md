---
name: 'ef-seed'
description: >
  Seed reference / master data for a module in packages/api/Tsz.Api. Two modes:
  EF Core HasData() in IEntityTypeConfiguration (static reference data baked into a
  migration), or a runtime ISeeder run at startup (dynamic / demo data). Use after
  backend-module when the entity needs baseline rows.
---

# EF Seed

Two-mode skill. Pick the mode that fits the data:

| Mode | When | Where it runs |
|---|---|---|
| `HasData()` | Reference data: lookups, enums-as-tables, fixed roles | Baked into a generated migration — runs once per environment |
| Runtime seeder | Demo data, dev fixtures, anything that may grow | Called from `Program.cs` after `Migrate()` — runs every startup, must be idempotent |

`AnimalSeeder` in the codebase today is a runtime seeder. New domains follow the same pattern unless the data is truly static reference data.

## Mode A — `HasData()` for static reference data

Use when the rows are fixed at design time, identified by stable IDs, and never change at runtime.

In `<Feature>Configuration.cs`:

```csharp
public class SpeciesConfiguration : IEntityTypeConfiguration<Species>
{
    public void Configure(EntityTypeBuilder<Species> builder)
    {
        builder.ToTable("Species");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(50);

        builder.HasData(
            new { Id = 1, Name = "Mammal" },
            new { Id = 2, Name = "Bird" },
            new { Id = 3, Name = "Reptile" });
    }
}
```

Then run the `ef-migration` skill (`dotnet ef migrations add SeedSpecies ...`). The migration's `Up()` will contain `migrationBuilder.InsertData(...)` calls; `Down()` will have the matching `DeleteData`. Editing the `HasData` later generates a new migration that diffs against the previous seed rows — EF handles inserts/updates/deletes automatically.

**Limits of HasData:**
- Cannot reference computed values (`DateTime.UtcNow`, `Guid.NewGuid()`) — IDs must be literals
- Cannot depend on other rows that aren't also HasData'd
- Awkward for hundreds of rows — code becomes noisy

## Mode B — runtime seeder at startup

Use when the data is demo/dev fixtures, or any case HasData can't cover. Pattern follows `AnimalSeeder`:

`Modules/<Feature>/<Feature>Seeder.cs`:

```csharp
namespace Tsz.Api.Modules.<Feature>;

public class <Feature>Seeder(<Feature>DbContext context)
{
    public void Seed()
    {
        if (context.<Feature>s.Any()) return; // idempotent guard

        context.<Feature>s.AddRange(
            <Feature>.Create(/* ... */),
            <Feature>.Create(/* ... */));
        context.SaveChanges();
    }
}
```

In `Program.cs`, after `Migrate()`:

```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AnimalDbContext>();
    if (db.Database.IsRelational())
        db.Database.Migrate();
    else
        db.Database.EnsureCreated();

    new <Feature>Seeder(db).Seed();
}
```

**Idempotency is your job.** The seeder runs on every startup — guard with `.Any()` or equivalent. Don't rely on EF migrations to prevent re-seeding.

## Step 1 — Pick the mode

- Is this lookup / reference data with fixed IDs? → Mode A
- Is this demo / dev data, or data with computed values? → Mode B

## Step 2 — Implement

Follow the section above for the chosen mode.

## Step 3 — For Mode A, generate the migration

`dotnet ef migrations add Seed<Feature> --project packages/api/Tsz.Api --startup-project packages/api/Tsz.Api --output-dir Migrations`

Inspect the generated `InsertData` calls. Commit.

## Step 4 — Verify

Start the API and confirm rows exist (Mode A: hit the endpoint; Mode B: same).

For integration tests that depend on baseline data: tests use `UseInMemoryDatabase`, so HasData seeds are NOT applied (InMemory doesn't run migrations). Use Mode B for anything tests need, or create the data inside each test class.
