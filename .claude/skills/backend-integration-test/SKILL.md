---
name: 'backend-integration-test'
description: >
  Add integration tests for an endpoint in packages/api/tests/Tsz.Api.Tests.Integration.
  Uses xUnit + Shouldly, NBuilder for fabricating POST bodies, WebApplicationFactory
  with UseInMemoryDatabase per fixture, TestAuthHandler bypass for [Authorize]. One
  test class per endpoint group.
---

# Backend Integration Test

Tests the HTTP surface of an endpoint against an in-memory EF Core database.
Each `IClassFixture<TestWebApplicationFactory>` gets a fresh in-memory DB named with a `Guid`, so test classes don't pollute each other. Within a class, tests share the same DB — write tests so they're order-independent (use unique values, don't rely on row counts from other tests).

## Conventions
- Test project: `packages/api/tests/Tsz.Api.Tests.Integration`
- xUnit (`[Fact]`, `IClassFixture<T>`, `Assert.*`); Shouldly available but the existing suite uses xUnit asserts — match the style of the file you're in
- `TestWebApplicationFactory` swaps `AnimalDbContext` to `UseInMemoryDatabase($"IntegrationTests_{Guid}")` and replaces auth with `TestAuthHandler`
- One test class per endpoint group: `AnimalEndpointsTests`, `<Feature>EndpointsTests`
- Seed data inside the test: `await SeedViaApi(...)` helpers that POST to the API, not external fixtures. Commands are positional records, so NBuilder's `Builder<TCommand>.CreateNew().Build()` handles them — every seed gets unique auto-filled values. Override only the field the test cares about.

## Step 1 — Clarify scope

- Which endpoint(s)?
- What's the expected status code for happy path, validation failure, not-found?
- Does the endpoint require any pre-existing rows? Create them in a private helper that hits the API (`POST /api/<feature>s`) so tests stay decoupled from the persistence layer.

## Step 2 — Test class

`<Feature>EndpointsTests.cs` (sibling of the existing `AnimalEndpointsTests.cs`):

```csharp
using System.Net;
using System.Net.Http.Json;
using Tsz.Api.Modules.<Feature>;
using Tsz.Api.Tests.Integration.TestAuth;

namespace Tsz.Api.Tests.Integration;

public class <Feature>EndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public <Feature>EndpointsTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<<Feature>Dto> SeedViaApiAsync(/* args with sensible defaults */)
    {
        var command = new Create<Feature>Command(/* ... */);
        var response = await _client.PostAsJsonAsync("/api/<feature>s", command);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<<Feature>Dto>())!;
    }

    [Fact]
    public async Task Get<Feature>s_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/<feature>s");

        response.EnsureSuccessStatusCode();
        var items = await response.Content.ReadFromJsonAsync<List<<Feature>Dto>>();
        Assert.NotNull(items);
    }

    [Fact]
    public async Task Create<Feature>_ValidRequest_ReturnsCreated()
    {
        var command = new Create<Feature>Command(/* valid values */);

        var response = await _client.PostAsJsonAsync("/api/<feature>s", command);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<<Feature>Dto>();
        Assert.NotNull(dto);
    }

    [Fact]
    public async Task Create<Feature>_InvalidRequest_ReturnsBadRequest()
    {
        var command = new Create<Feature>Command(/* invalid values */);

        var response = await _client.PostAsJsonAsync("/api/<feature>s", command);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get<Feature>ById_NonExisting_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/<feature>s/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
```

## Step 3 — If your feature uses a new DbContext

`TestWebApplicationFactory.ConfigureWebHost` only swaps `AnimalDbContext` today. If the new module has its own DbContext, add a second swap block in `TestWebApplicationFactory`:

```csharp
var toRemove2 = services
    .Where(d =>
        d.ServiceType == typeof(DbContextOptions<<Feature>DbContext>) ||
        d.ServiceType == typeof(IDbContextOptionsConfiguration<<Feature>DbContext>) ||
        d.ServiceType == typeof(<Feature>DbContext))
    .ToList();
foreach (var d in toRemove2)
    services.Remove(d);

services.AddDbContext<<Feature>DbContext>(options =>
    options.UseInMemoryDatabase(_dbName));
```

## Step 4 — Run

```
bun run test:api:int
```

Or directly:
```
dotnet test packages/api/tests/Tsz.Api.Tests.Integration
```

## Notes on InMemory vs real SQLite

These tests run against `UseInMemoryDatabase`, **not** SQLite. That means:
- `db.Database.Migrate()` is NOT called (InMemory doesn't support migrations) — the model is materialized from the DbContext at runtime
- `HasData()` seeds from `IEntityTypeConfiguration` are NOT applied (they only run via migrations) — seed via API calls inside the test
- Provider-specific SQL (raw `migrationBuilder.Sql`, SQLite functions like `datetime('now')`) won't fire — anything that depends on those won't be covered here

If you ever need to exercise actual migration behaviour or a raw SQL block, that's a separate test that spins up a real `SqliteConnection` and calls `db.Database.Migrate()`. Out of scope for this skill.
