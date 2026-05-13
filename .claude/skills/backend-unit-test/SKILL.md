---
name: 'backend-unit-test'
description: >
  Add unit tests for a handler or validator in packages/api/tests/Tsz.Api.Tests.
  Uses xUnit, Moq for IUnitOfWork and IRepository<T>, Shouldly assertions, and
  per-entity builders (Builders/<Entity>Builder.cs) for fixture data.
  Tests in isolation - no HTTP, no real DB.
paths: packages/api/**
---

# Backend Unit Test

Tests a single handler or validator class in isolation using xUnit + Moq + Shouldly + per-entity builders.

## Conventions
- Test class mirrors the SUT: `CreateAnimalHandlerTests` tests `CreateAnimalHandler`
- Validators get their own file alongside the handler tests: `CreateAnimalValidatorTests`
- One `[Fact]` per meaningful scenario (happy path + key failure cases)
- Use Shouldly for assertions (`result.ShouldBe(...)`, `task.ShouldThrowAsync<...>()`)
- Use Moq for dependencies — `Mock<IRepository<T>>`, `Mock<IUnitOfWork>`
- Use the per-entity builder under `tests/Tsz.Api.Tests/Builders/<Entity>Builder.cs` to construct entities — `AnimalBuilder.Build().WithName("Rex")`. The builder calls `Animal.Create(...)` and the named mutators, so invariants stay enforced. Each `Build()` produces unique randomized defaults via NBuilder so callers only spell out the fields that matter.
- For DTO lists where values don't matter (mock returns), use NBuilder directly: `Builder<<Feature>Dto>.CreateListOfSize(3).Build()`. Positional records work via NBuilder's ctor-param synthesis.
- Test project: `packages/api/tests/Tsz.Api.Tests`, namespace `Tsz.Api.Tests.Modules.<Feature>`

## Step 1 — Clarify scope

- Which handler/validator?
- What are the meaningful scenarios? (happy path, not-found, validation failure, etc.)
- What dependencies does the handler take in its constructor?

## Step 2 — Handler test shape

`Modules/<Feature>/<Operation><Feature>HandlerTests.cs`:

```csharp
using Tsz.Api.Modules.<Feature>;
using Tsz.Infrastructure.Abstractions;
using Moq;
using Shouldly;

namespace Tsz.Api.Tests.Modules.<Feature>;

public class <Operation><Feature>HandlerTests
{
    [Fact]
    public async Task HandleAsync_<Scenario>_<Expectation>()
    {
        var repo = new Mock<IRepository<<Feature>>>();
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<<Feature>>()).Returns(repo.Object);

        var handler = new <Operation><Feature>Handler(uow.Object);

        var result = await handler.HandleAsync(new <Operation><Feature>Command(/* ... */));

        result.ShouldNotBeNull();
        repo.Verify(r => r.Add(It.IsAny<<Feature>>()), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

## Step 3 — Not-found / negative-path test

```csharp
[Fact]
public async Task HandleAsync_Missing_ReturnsNull()
{
    var repo = new Mock<IRepository<<Feature>>>();
    repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync((<Feature>?)null);
    var uow = new Mock<IUnitOfWork>();
    uow.Setup(u => u.RepositoryFor<<Feature>>()).Returns(repo.Object);

    var handler = new Update<Feature>Handler(uow.Object);

    var result = await handler.HandleAsync(new Update<Feature>Command(Guid.NewGuid(), /* ... */));

    result.ShouldBeNull();
    uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
}
```

## Step 4 — Validator test shape

`Modules/<Feature>/<Operation><Feature>ValidatorTests.cs`:

```csharp
using Tsz.Api.Modules.<Feature>;
using Shouldly;

namespace Tsz.Api.Tests.Modules.<Feature>;

public class <Operation><Feature>ValidatorTests
{
    [Fact]
    public async Task Valid_PassesValidation()
    {
        var validator = new <Operation><Feature>Validator();
        var result = await validator.ValidateAsync(new <Operation><Feature>Command(/* valid */));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task <BrokenField>_Fails()
    {
        var validator = new <Operation><Feature>Validator();
        var result = await validator.ValidateAsync(new <Operation><Feature>Command(/* broken */));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(<Operation><Feature>Command.<Field>));
    }
}
```

## Step 5 — Query handler tests

Reads typically mock `FirstOrDefaultAsDtoAsync<TDto>` / `GetAllAsDtosAsync<TDto>`. Use NBuilder to fabricate the DTOs the repo will return — positional records are fine here, NBuilder synthesizes ctor args:

```csharp
using FizzWare.NBuilder;

var dtos = Builder<<Feature>Dto>.CreateListOfSize(3).Build();

repo.Setup(r => r.FirstOrDefaultAsDtoAsync<<Feature>Dto>(
        It.IsAny<System.Linq.Expressions.Expression<Func<<Feature>, bool>>>(),
        It.IsAny<CancellationToken>()))
    .ReturnsAsync(dtos[0]);
```

For *entities* (private setters, factory + named mutators), use the per-entity builder instead — NBuilder's `.With(x => x.Prop = ...)` won't compile against private setters.

## Step 6 — Run tests

```
bun run test:api
```

Fix any Shouldly / Moq compilation errors, then `bun run build:api` to confirm no type regressions.
