---
name: 'backend-integration-test'
description: >
  Add integration tests for an endpoint in packages/api/Tsz.Api.Tests.Integration.
  Uses xUnit + IntegrationTestBase (Client + WithUowAsync helpers), WebApplicationFactory
  with UseInMemoryDatabase per fixture, TestAuthHandler bypass for [Authorize]. One test
  class per endpoint group, derived from IntegrationTestBase. All DB access via
  IUnitOfWork / IRepository — no direct AppDbContext access.
paths: packages/api/**
---

# Backend Integration Test

Tests the HTTP surface of an endpoint against an in-memory EF Core database.
Each test class is an `IntegrationTestBase` that shares a `TestWebApplicationFactory` via `IClassFixture`. The factory swaps `AppDbContext` to `UseInMemoryDatabase($"IntegrationTests_{Guid}")` and installs `TestAuthHandler` so `[Authorize]` is satisfied. Tests within a class share the same DB — use `IAsyncLifetime.InitializeAsync` to purge rows between tests.

## Conventions
- Test project: `packages/api/Tsz.Api.Tests.Integration`
- xUnit (`[Fact]`, `Assert.*`); Shouldly available — match the style of the file you're in
- One test class per endpoint group: `<Feature>EndpointsTests`, inheriting `IntegrationTestBase`
- Cleanup between tests via `IAsyncLifetime.InitializeAsync` — call `repo.BatchHardDeleteAsync(_ => true)` (it ignores soft-delete query filters, so soft-deleted rows are wiped too)
- Seed and query through **`IUnitOfWork`** using the `WithUowAsync(...)` helper from the base. The base also exposes `Client` and a shared `Json` options object with `JsonStringEnumConverter`.
- To read soft-deleted rows, pass `ignoreQueryFilters: true` on the repo call
- `UseInMemoryDatabase` is shared per `TestWebApplicationFactory` instance (xUnit gives each test class its own factory via `IClassFixture`), so classes don't pollute each other

## Step 1 — Clarify scope

- Which endpoint(s)?
- What's the expected status code for happy path, validation failure, not-found, forbidden?
- Does the endpoint require pre-existing rows or a specific authenticated user? Seed them in `InitializeAsync` or in a private helper that uses `WithUowAsync(...)`.

## Step 2 — Test class

`<Feature>EndpointsTests.cs` (sibling of the existing `UserEndpointsTests.cs`):

```csharp
using System.Net;
using System.Net.Http.Json;
using Tsz.Api.Modules.<Feature>;
using Tsz.Api.Modules.<Feature>.Features;
using Tsz.Api.Tests.Integration.TestAuth;

namespace Tsz.Api.Tests.Integration;

public class <Feature>EndpointsTests : IntegrationTestBase, IAsyncLifetime
{
    public <Feature>EndpointsTests(TestWebApplicationFactory factory) : base(factory) { }

    public Task InitializeAsync() => WithUowAsync(uow =>
        uow.RepositoryFor<<Feature>>().BatchHardDeleteAsync(_ => true));

    public Task DisposeAsync() => Task.CompletedTask;

    private Task Seed<Feature>Async(/* args with sensible defaults */) => WithUowAsync(async uow =>
    {
        var entity = <Feature>.Create(/* ... */);
        uow.RepositoryFor<<Feature>>().Add(entity);
        await uow.SaveChangesAsync();
    });

    [Fact]
    public async Task Get<Feature>s_ReturnsOk()
    {
        await Seed<Feature>Async();

        var response = await Client.GetAsync("/api/<feature>s");

        response.EnsureSuccessStatusCode();
        var items = await response.Content.ReadFromJsonAsync<List<<Feature>Dto>>(Json);
        Assert.NotNull(items);
    }

    [Fact]
    public async Task Create<Feature>_ValidRequest_ReturnsCreated()
    {
        var command = new Create<Feature>Command(/* valid values */);

        var response = await Client.PostAsJsonAsync("/api/<feature>s", command, Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<<Feature>Dto>(Json);
        Assert.NotNull(dto);
    }

    [Fact]
    public async Task Create<Feature>_InvalidRequest_ReturnsBadRequest()
    {
        var command = new Create<Feature>Command(/* invalid values */);

        var response = await Client.PostAsJsonAsync("/api/<feature>s", command, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Json);
        Assert.NotNull(problem);
        Assert.NotNull(problem.Errors);
    }

    [Fact]
    public async Task Create<Feature>_BusinessRuleViolation_ReturnsConflict()
    {
        // Seed a feature that violates uniqueness
        await Seed<Feature>Async();

        var command = new Create<Feature>Command(/* values that conflict */);

        var response = await Client.PostAsJsonAsync("/api/<feature>s", command, Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Json);
        Assert.NotNull(problem);
        Assert.Equal("<Feature>Errors.ConflictCode", problem.Code);
    }

    [Fact]
    public async Task Get<Feature>ById_NonExisting_ReturnsNotFound()
    {
        var response = await Client.GetAsync($"/api/<feature>s/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
```

For seeding nuance — entities with private setters need the static `Create(...)` factory, then optionally calling named mutators (e.g. `user.LinkEntraOid(...)`) before `Add` + `SaveChangesAsync`. See `UserEndpointsTests.SeedUserAsync` for the live shape.

## Step 3 — Reading soft-deleted rows

Soft-delete query filters (`HasQueryFilter`) hide deleted rows from every repo read by default. To assert a row was soft-deleted (vs. hard-deleted), or to read a soft-deleted row's state, pass `ignoreQueryFilters: true`:

```csharp
var row = await WithUowAsync(uow =>
    uow.RepositoryFor<User>().FirstOrDefaultAsync(u => u.Id == id, ignoreQueryFilters: true));
Assert.NotNull(row);
Assert.NotNull(row.DeletedAt);
```

`BatchHardDeleteAsync` always bypasses query filters internally — it's the bulk-purge primitive used by test cleanup.

## Step 4 — Auth-sensitive scenarios

`TestAuthHandler` provisions a default test principal. If your endpoint depends on the authenticated user (e.g. `GET /me`) or on a role policy (`AuthorizationPolicies.RequireAdmin`), check `UserEndpointsTests` for examples of seeding users with specific OIDs / roles to satisfy the policy. The factory already wires `TestAuthHandler` as the default scheme; you don't need to configure auth per test.

## Step 5 — Run

```
bun run test:api:int
```

Or directly:
```
dotnet test packages/api/Tsz.Api.Tests.Integration
```

## Validation & Error Testing

Endpoints use `IDispatcher` for commands (queries call handlers directly). Validation failures throw `FluentValidation.ValidationException`, caught by the global exception handler (`Tsz.Api/Infrastructure/GlobalExceptionHandler.cs`) and emitted as RFC 7807 ProblemDetails. Parse and assert on:
- `problem.Code` — top-level error code (highest-severity error)
- `problem.Status` — HTTP status (derived from error categories: NotFound → 404, Conflict → 409, Validation → 400, etc.)
- `problem.Errors` — per-property map of `{ code, message }[]`

See `UserEndpointsTests` for live examples asserting validation and conflict scenarios.

## Notes on InMemory vs real SQLite

These tests run against `UseInMemoryDatabase`, **not** SQLite. That means:
- `db.Database.Migrate()` is NOT called — the model is materialized from `AppDbContext` at runtime
- `HasData()` seeds from `IEntityTypeConfiguration` are NOT applied (they only run via migrations) — seed via `WithUowAsync` inside the test
- Provider-specific SQL (raw `migrationBuilder.Sql`, SQLite functions like `datetime('now')`, partial-index `HasFilter` clauses) won't fire — anything that depends on those won't be covered here
- `ExecuteDeleteAsync` is relational-only; `BatchHardDeleteAsync` detects this and falls back to load+RemoveRange+SaveChanges on InMemory automatically — same observable behaviour, just less efficient

If you ever need to exercise actual migration behaviour or a raw SQL block, that's a separate test that spins up a real `SqliteConnection` and calls `db.Database.Migrate()`. Out of scope for this skill.
