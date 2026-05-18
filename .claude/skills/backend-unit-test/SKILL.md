---
name: 'backend-unit-test'
description: >
  Add unit tests for a handler or validator in packages/api/Tsz.Api.Tests.
  Uses xUnit, Moq for IUnitOfWork and IRepository<T>, Shouldly assertions, and
  per-entity builders (Builders/<Entity>Builder.cs) for fixture data.
  Tests in isolation - no HTTP, no real DB.
paths: packages/api/**
---

# Backend Unit Test

Tests a single handler or validator class in isolation using xUnit + Moq + Shouldly + per-entity builders.

## Conventions
- Test project: `packages/api/Tsz.Api.Tests`
- Mirrors the source layout: `Modules/<Feature>/Features/<Operation><Feature>HandlerTests.cs` next to the matching `…ValidatorTests.cs`
- Namespace: `Tsz.Api.Tests.Modules.<Feature>.Features`
- One `[Fact]` per meaningful scenario (happy path + key failure cases)
- Use Shouldly for assertions (`result.ShouldBe(...)`, `task.ShouldThrowAsync<...>()`)
- Use Moq for dependencies — `Mock<IRepository<T>>`, `Mock<IUnitOfWork>`
- Use the per-entity builder under `Tsz.Api.Tests/Builders/<Entity>Builder.cs` to construct entities — `UserBuilder.Build().WithName("Jane")`. The builder calls `Entity.Create(...)` and named mutators, so invariants stay enforced. Each `Build()` produces unique randomized defaults so callers only spell out the fields that matter.
- For DTO lists where values don't matter (mock returns), use NBuilder directly: `Builder<<Feature>Dto>.CreateListOfSize(3).Build()`. Positional records work via NBuilder's ctor-param synthesis.

## Step 1 — Clarify scope

- Which handler/validator?
- What are the meaningful scenarios? (happy path, not-found, validation failure, conflict, etc.)
- What dependencies does the handler take in its constructor?

## Step 2 — Handler test shape

`Modules/<Feature>/Features/<Operation><Feature>HandlerTests.cs`:

```csharp
using Moq;
using Shouldly;
using Tsz.Api.Modules.<Feature>;
using Tsz.Api.Modules.<Feature>.Features;
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Tests.Modules.<Feature>.Features;

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

Handlers return plain DTOs (no custom result records). Live reference: `CreateUserHandlerTests` mirrors this shape. Validation errors are tested separately in the validator test class — handlers assume the command passed validation.

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

`Modules/<Feature>/Features/<Operation><Feature>ValidatorTests.cs`:

```csharp
using Moq;
using Shouldly;
using Tsz.Api.Modules.<Feature>;
using Tsz.Api.Modules.<Feature>.Features;
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Tests.Modules.<Feature>.Features;

public class <Operation><Feature>ValidatorTests
{
    [Fact]
    public async Task Valid_Passes()
    {
        var uow = new Mock<IUnitOfWork>();
        var validator = new <Operation><Feature>Validator(uow.Object);
        var result = await validator.ValidateAsync(new <Operation><Feature>Command(/* valid */));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task <BrokenField>_Fails()
    {
        var uow = new Mock<IUnitOfWork>();
        var validator = new <Operation><Feature>Validator(uow.Object);
        var result = await validator.ValidateAsync(new <Operation><Feature>Command(/* broken */));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(<Operation><Feature>Command.<Field>));
    }

    [Fact]
    public async Task BusinessRuleViolation_Fails()
    {
        var repo = new Mock<IRepository<<Feature>>>();
        repo.Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<<Feature>, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true); // e.g., email already exists
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<<Feature>>()).Returns(repo.Object);

        var validator = new <Operation><Feature>Validator(uow.Object);
        var result = await validator.ValidateAsync(new <Operation><Feature>Command(/* values */));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "<Feature>Errors.Code");
    }
}
```

Validators may inject `IUnitOfWork` for async business-rule checks (e.g., uniqueness). Use `.WithError(ErrorCodeBase)` to embed error codes with categories so the global handler can route to the correct HTTP status. Live reference: `CreateUserValidatorTests`.

## Step 5 — Query handler tests

Reads typically mock `FirstOrDefaultAsDtoAsync<TDto>` / `GetAllAsDtosAsync<TDto>`. Use NBuilder to fabricate the DTOs the repo will return — positional records work, NBuilder synthesizes ctor args:

```csharp
using FizzWare.NBuilder;
using System.Linq.Expressions;

var dtos = Builder<<Feature>Dto>.CreateListOfSize(3).Build();

repo.Setup(r => r.FirstOrDefaultAsDtoAsync<<Feature>Dto>(
        It.IsAny<Expression<Func<<Feature>, bool>>>(),
        It.IsAny<CancellationToken>()))
    .ReturnsAsync(dtos[0]);
```

For *entities* (private setters, factory + named mutators), use the per-entity builder instead — NBuilder's `.With(x => x.Prop = ...)` won't compile against private setters.

## Step 6 — Run tests

```
bun run test:api
```

Fix any Shouldly / Moq compilation errors, then `bun run build:api` to confirm no type regressions.
