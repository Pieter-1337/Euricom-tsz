---
name: 'ef-migration'
description: >
  Add an EF Core migration after changing an entity or its IEntityTypeConfiguration in
  packages/api/Tsz.Api. Migrations are generated from the C# model via `dotnet ef`,
  applied automatically at API startup via `db.Database.Migrate()`, and committed to
  the repo (including the ModelSnapshot). Use whenever a schema change is needed.
paths: packages/api/**
---

# EF Migration

The source of truth is the C# model (entity + `IEntityTypeConfiguration<T>`). Migrations are generated, not hand-written. They live in `packages/api/Tsz.Api/Migrations/` and are applied at startup by `Program.cs`.

## Conventions
- DbContext: `Tsz.Api/Modules/Animals/AnimalDbContext.cs` (one context for the whole API right now)
- Migration folder: `packages/api/Tsz.Api/Migrations/`
- Migrations are applied at startup via `db.Database.Migrate()` (guarded by `IsRelational()` so InMemory tests don't trip on it)
- Tool: `dotnet ef` is a local tool — installed via `dotnet-tools.json` at the repo root
- **Never edit a migration that has already been applied to any environment.** Generate a new one to make further changes.

## Step 1 — Clarify scope
- What changed on the entity / configuration? (new field, new index, rename, type change, …)
- A descriptive PascalCase name for the migration (`AddColourToAnimal`, `IndexAnimalSpecies`, …)
- Does existing data need to be backfilled? If so, the generated migration will need a hand-edited `migrationBuilder.Sql(...)` call.

## Step 2 — Edit the model

Modify the entity in `packages/api/Tsz.Api/Modules/<Feature>/<Feature>.cs` and/or its `<Feature>Configuration.cs`. Run `bun run check` to make sure it still compiles before generating the migration — `dotnet ef` builds the project, so a compile error blocks generation.

## Step 3 — Generate the migration

From the repo root:

```
dotnet ef migrations add <Name> \
  --project packages/api/Tsz.Api \
  --startup-project packages/api/Tsz.Api \
  --output-dir Migrations
```

This creates three files in `Tsz.Api/Migrations/`:
- `<timestamp>_<Name>.cs` — `Up()` / `Down()` with the generated `migrationBuilder.*` calls
- `<timestamp>_<Name>.Designer.cs` — generated, do not edit
- `AnimalDbContextModelSnapshot.cs` — updated snapshot of the full model; **commit this**

## Step 4 — Inspect and hand-edit if needed

Open `<timestamp>_<Name>.cs` and read the generated SQL. Two SQLite-specific things to watch for:

1. **`DROP COLUMN` / type change / `ALTER` of constraints** — EF emulates these via the "create new table, copy data, drop, rename" dance. The migration is large but correct. If you have lots of data, consider whether the change is worth it.
2. **Data backfill** — EF only generates DDL. If a new `NOT NULL` column needs values for existing rows, add a `migrationBuilder.Sql("UPDATE ...")` between the `AddColumn` and any later step. Mirror it in `Down()`.

## Step 5 — Apply locally

Stop the API if it's running (`animals.db` will be locked otherwise), then start it again — `db.Database.Migrate()` runs at startup and applies any pending migrations. To apply without starting the API:

```
dotnet ef database update \
  --project packages/api/Tsz.Api \
  --startup-project packages/api/Tsz.Api
```

## Step 6 — Verify

```
bun run test:api:int
```

Integration tests use `UseInMemoryDatabase` and won't apply migrations — they just exercise the model. If you've added a new entity and want it covered, see `backend-integration-test`.

## Reverting a not-yet-applied migration

If the migration was generated but never run (no environment has it yet):

```
dotnet ef migrations remove --project packages/api/Tsz.Api --startup-project packages/api/Tsz.Api
```

If it was applied locally, first roll back: `dotnet ef database update <PreviousMigrationName>`, then remove.
