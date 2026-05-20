---
name: 'backend-module'
description: >
  Scaffold a new domain module as separate classibs in packages/api/Modules/<Feature>/:
  Tsz.Modules.<Feature> (impl) and Tsz.Modules.<Feature>.Contracts (public surface).
  DDD-ish entity (private setters, static Create factory, named mutators), EF config
  (auto-discovered), <Feature>Module : IModule for DI wiring, module-level facade,
  endpoint file, and Program.cs registration. No DbSet plumbing — all access via
  IUnitOfWork / IRepository. Use once per new domain concept before adding slices.
paths: packages/api/**
---

# Backend Module

One-time scaffold for a new domain module across two classibs. Run this first, then use `backend-slice` to add operations. Users is the canonical live example to mirror.

## Conventions
- Module lives in `packages/api/Modules/<Feature>/Tsz.Modules.<Feature>/` (impl) and `packages/api/Modules/<Feature>/Tsz.Modules.<Feature>.Contracts/` (public surface)
- Impl namespace: `Tsz.Modules.<Feature>` and nested sub-namespaces (`Features`, `Domain.<Aggregate>`, `Seeding`, `Auth`, `Endpoints`)
- Contracts namespace: `Tsz.Modules.<Feature>.Contracts` and sub-namespaces (`Queries`, etc.)
- Dependency rule: impl can reference other modules' Contracts; never another module's impl
- Entity folder structure: `Domain/<Aggregate>/` houses entity, `<Aggregate>Configuration.cs`, DTO, and error code class
- Entity implements `IEntityBase` (`Guid Id`); writable state has **private setters**; construction via `static Create(...)`; mutation via named methods (`Rename`, `ChangeRole`, …)
- EF config in `Domain/<Aggregate>/<Aggregate>Configuration.cs` implementing `IEntityTypeConfiguration<T>` — picked up automatically by `AppDbContext.OnModelCreating` via `ApplyConfigurationsFromAssembly`. Nothing else needs to be done to register the entity — `AppDbContext` has no explicit `DbSet<T>` properties; everything flows through `context.Set<T>()` inside the repo.
- One shared `AppDbContext` at `Tsz.Api/Persistence/AppDbContext.cs` for the whole API — never spin up a per-module DbContext
- All persistence access from slices, seeders, and tests goes through `IUnitOfWork` → `IRepository<T>` (in `Tsz.Infrastructure.Abstractions`). Direct `AppDbContext` access is reserved for `Program.cs` startup wiring (`Migrate`/`EnsureCreated`).
- Each module has a `<Feature>Module : IModule` class at the impl root with `RegisterServices(services, config)` and `MapEndpoints(app)` methods
- Each module also ships a `<Feature>AccessModule : I<Feature>AccessModule` (facade) wrapping `IDispatcher` for cross-module queries/commands
- Endpoints file: `Endpoints/<Feature>Endpoints.cs` — one static class, one `Map()` method
- Register the module in `Tsz.Api/Program.cs` by adding it to the `IReadOnlyList<IModule>` array
- Update `AppDbContext.OnModelCreating` with `modelBuilder.ApplyConfigurationsFromAssembly(typeof(<Feature>Module).Assembly)`
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

`Modules/<Feature>/Tsz.Modules.<Feature>/Domain/<Aggregate>/<Aggregate>.cs`:
```csharp
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Modules.<Feature>.Domain.<Aggregate>;

public class <Aggregate> : IEntityBase
{
    public Guid Id { get; set; }
    public string Name { get; private set; } = string.Empty;
    // additional properties with private setters

    private <Aggregate>() { } // EF

    public static <Aggregate> Create(string name /*, ... */) => new()
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

`Modules/<Feature>/Tsz.Modules.<Feature>/Domain/<Aggregate>/<Aggregate>Configuration.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsz.Modules.<Feature>.Domain.<Aggregate>;

namespace Tsz.Modules.<Feature>.Domain.<Aggregate>;

public class <Aggregate>Configuration : IEntityTypeConfiguration<<Aggregate>>
{
    public void Configure(EntityTypeBuilder<<Aggregate>> builder)
    {
        builder.ToTable("<Aggregate>s");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        // additional constraints, indexes
    }
}
```

No registration needed — `AppDbContext` calls `ApplyConfigurationsFromAssembly` and picks it up automatically from the module assembly.

## Step 4 — DbContext registration

Edit `Tsz.Api/Persistence/AppDbContext.cs` and add a call to `ApplyConfigurationsFromAssembly` for your new module:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(UsersModule).Assembly);
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(CustomersModule).Assembly);
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(<Feature>Module).Assembly);  // ADD THIS
}
```

`AppDbContext` will discover your new `<Aggregate>Configuration` and register the entity. No `DbSet<T>` property is needed — slices, seeders, and tests reach the entity via `IUnitOfWork.RepositoryFor<<Aggregate>>()`, which internally calls `context.Set<<Aggregate>>()`.

## Step 5 — Endpoint file shell

`Modules/<Feature>/Tsz.Modules.<Feature>/Endpoints/<Feature>Endpoints.cs`:
```csharp
using Microsoft.AspNetCore.Routing;
using Tsz.Infrastructure.Endpoints;

namespace Tsz.Modules.<Feature>.Endpoints;

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

## Step 6 — Module class

Create `Modules/<Feature>/Tsz.Modules.<Feature>/<Feature>Module.cs`:

```csharp
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tsz.Infrastructure.Extensions;
using Tsz.Modules.<Feature>.Contracts;
using Tsz.Modules.<Feature>.Endpoints;

namespace Tsz.Modules.<Feature>;

public sealed class <Feature>Module : IModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHandlersFromAssembly(typeof(<Feature>Module).Assembly);
        services.AddValidatorsFromAssembly(typeof(<Feature>Module).Assembly);
        services.AddScoped<I<Feature>AccessModule, <Feature>AccessModule>();
        // add module-specific DI (seeders, auth handlers, etc.)
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        <Feature>Endpoints.Map(app);
    }
}
```

The module is responsible for registering its own services and endpoints.

## Step 7 — Module access facade

Create `Modules/<Feature>/Tsz.Modules.<Feature>/<Feature>AccessModule.cs`:

```csharp
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Cqrs;
using Tsz.Modules.<Feature>.Contracts;

namespace Tsz.Modules.<Feature>;

internal sealed class <Feature>AccessModule(IDispatcher dispatcher) : I<Feature>AccessModule
{
    public Task<TResult> ExecuteQueryAsync<TResult>(IQuery<TResult> query, CancellationToken ct = default) =>
        dispatcher.SendAsync(query, ct);

    public Task<TResult> ExecuteCommandAsync<TResult>(ICommand<TResult> command, CancellationToken ct = default) =>
        dispatcher.SendAsync(command, ct);
}
```

And the interface in `Modules/<Feature>/Tsz.Modules.<Feature>.Contracts/I<Feature>AccessModule.cs`:

```csharp
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Modules.<Feature>.Contracts;

public interface I<Feature>AccessModule
{
    Task<TResult> ExecuteQueryAsync<TResult>(IQuery<TResult> query, CancellationToken ct = default);
    Task<TResult> ExecuteCommandAsync<TResult>(ICommand<TResult> command, CancellationToken ct = default);
}
```

This facade allows other modules to dispatch cross-module queries/commands through a clean, typed interface.

## Step 8 — Register in Program.cs

In `Tsz.Api/Program.cs`, add your module to the `IReadOnlyList<IModule>` array:

```csharp
// Near the top of Program.cs, after other imports
using Tsz.Modules.<Feature>;

// ...

IReadOnlyList<IModule> modules = [
    new UsersModule(), 
    new CustomersModule(),
    new <Feature>Module()  // ADD THIS
];
foreach (var m in modules) m.RegisterServices(builder.Services, builder.Configuration);

// ...

var app = builder.Build();

// Wire endpoints
foreach (var m in modules) m.MapEndpoints(app);
```

## Step 9 — Update AppDbContext

In `Tsz.Api/Persistence/AppDbContext.cs`, add the module's assembly:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(UsersModule).Assembly);
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(CustomersModule).Assembly);
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(<Feature>Module).Assembly);  // ADD THIS
}
```

## Step 10 — Create the two .csproj files

**Impl csproj** — `Modules/<Feature>/Tsz.Modules.<Feature>/Tsz.Modules.<Feature>.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <Version>1.0.0</Version>
  </PropertyGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="Tsz.Api.Tests" />
    <InternalsVisibleTo Include="Tsz.Api.Tests.Integration" />
  </ItemGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Relational" />
    <PackageReference Include="FluentValidation" />
    <PackageReference Include="FluentValidation.DependencyInjectionExtensions" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include=".\Tsz.Modules.<Feature>.Contracts\Tsz.Modules.<Feature>.Contracts.csproj" />
    <ProjectReference Include="..\..\..\Tsz.Infrastructure\Tsz.Infrastructure.csproj" />
  </ItemGroup>

  <!-- Add ProjectReference to other modules' Contracts ONLY, never their impl -->
  <!-- Example: <ProjectReference Include="..\Users\Tsz.Modules.Users.Contracts\Tsz.Modules.Users.Contracts.csproj" /> -->
</Project>
```

**Contracts csproj** — `Modules/<Feature>/Tsz.Modules.<Feature>.Contracts/Tsz.Modules.<Feature>.Contracts.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <Version>1.0.0</Version>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\..\Tsz.Infrastructure\Tsz.Infrastructure.csproj" />
  </ItemGroup>
</Project>
```

Then update `Tsz.Api.csproj`, `Tsz.Api.Tests.csproj`, and `Tsz.Api.Tests.Integration.csproj` to reference both:

```xml
<ProjectReference Include="..\Modules\<Feature>\Tsz.Modules.<Feature>\Tsz.Modules.<Feature>.csproj" />
<ProjectReference Include="..\Modules\<Feature>\Tsz.Modules.<Feature>.Contracts\Tsz.Modules.<Feature>.Contracts.csproj" />
```

## Step 11 — Test builder

Every entity gets a builder in `packages/api/Tsz.Api.Tests/Builders/<Aggregate>Builder.cs`. The builder calls the static `Create(...)` factory (so invariants stay enforced) and exposes one `WithXxx` extension per named mutator.

```csharp
using FizzWare.NBuilder.Generators;
using Tsz.Modules.<Feature>.Domain.<Aggregate>;

namespace Tsz.Api.Tests.Builders;

public static class <Aggregate>Builder
{
    public static <Aggregate> Build()
    {
        var id = Guid.NewGuid();
        var entity = <Aggregate>.Create(
            name: "Name_" + id.ToString()[..8]
            /* ... fill remaining required Create params */);
        entity.Id = id;
        return entity;
    }

    public static <Aggregate> WithId(this <Aggregate> entity, Guid id)
    {
        entity.Id = id;
        return entity;
    }

    public static <Aggregate> WithName(this <Aggregate> entity, string name)
    {
        entity.Rename(name);
        return entity;
    }

    // one WithXxx per named mutator on the entity
}
```

`UserBuilder.cs` is the canonical example. Use `GetRandom.Int(min, max)` / `GetRandom.Email()` / `GetRandom.AlphaString(n)` for primitives. Builders ship sensible defaults so test call sites only spell out the field they care about: `UserBuilder.Build().WithRole(UserRole.Admin)`.

## Step 12 — Generate the migration

The new entity needs a schema. From the repo root:

```
dotnet ef migrations add Add<Feature> \
  --project packages/api/Tsz.Api \
  --startup-project packages/api/Tsz.Api \
  --output-dir Persistence/Migrations
```

Inspect the generated `Up()` to confirm the `CreateTable` looks right. Commit the migration + updated `AppDbContextModelSnapshot.cs`. Migrations run automatically on next API start via `db.Database.Migrate()` in `Program.cs`. See the `ef-migration` skill for hand-edit guidance (data backfills, SQLite ALTER limits).

Note: `dotnet ef` discovers the entity via the configuration's `ApplyConfigurationsFromAssembly` call, not via a `DbSet<T>` property.

## Step 13 — Verify

Run `bun run check`. Then add operations with `backend-slice`.
