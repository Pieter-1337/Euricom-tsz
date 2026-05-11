using Api.Common.Filters;
using Api.Modules.Animals;
using Microsoft.AspNetCore.Http;

namespace Api.Tests.Common.Filters;

public class ValidationFilterTests
{
    private sealed class FakeFilterContext(params object?[] args) : EndpointFilterInvocationContext
    {
        public override HttpContext HttpContext { get; } = new DefaultHttpContext();
        public override IList<object?> Arguments { get; } = [.. args];
        public override T GetArgument<T>(int index) => (T)args[index]!;
    }

    private static EndpointFilterDelegate NextReturning(object? value) =>
        _ => ValueTask.FromResult(value);

    private static int GetResultStatus(object? result) =>
        Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode ?? 200;

    [Fact]
    public async Task InvokeAsync_MissingArgument_ReturnsBadRequest()
    {
        var filter = new ValidationFilter<CreateAnimalRequest>();
        var context = new FakeFilterContext();

        var result = await filter.InvokeAsync(context, NextReturning("sentinel"));

        Assert.Equal(400, GetResultStatus(result));
    }

    [Fact]
    public async Task InvokeAsync_ValidModel_CallsNext()
    {
        var filter = new ValidationFilter<CreateAnimalRequest>();
        var model = new CreateAnimalRequest { Name = "Buddy", Species = "Dog", Age = 3 };
        var context = new FakeFilterContext(model);
        var sentinel = new object();

        var result = await filter.InvokeAsync(context, NextReturning(sentinel));

        Assert.Same(sentinel, result);
    }

    [Fact]
    public async Task InvokeAsync_EmptyRequiredField_ReturnsValidationProblem()
    {
        var filter = new ValidationFilter<CreateAnimalRequest>();
        var model = new CreateAnimalRequest { Name = "", Species = "Dog", Age = 3 };
        var context = new FakeFilterContext(model);

        var result = await filter.InvokeAsync(context, NextReturning("sentinel"));

        Assert.Equal(400, GetResultStatus(result));
    }

    [Fact]
    public async Task InvokeAsync_AgeOutOfRange_ReturnsValidationProblem()
    {
        var filter = new ValidationFilter<CreateAnimalRequest>();
        var model = new CreateAnimalRequest { Name = "Rex", Species = "Dog", Age = -1 };
        var context = new FakeFilterContext(model);

        var result = await filter.InvokeAsync(context, NextReturning("sentinel"));

        Assert.Equal(400, GetResultStatus(result));
    }

    [Fact]
    public async Task InvokeAsync_MultipleInvalidFields_DoesNotCallNext()
    {
        var filter = new ValidationFilter<CreateAnimalRequest>();
        var model = new CreateAnimalRequest { Name = "", Species = "", Age = 999 };
        var context = new FakeFilterContext(model);
        var nextCalled = false;
        EndpointFilterDelegate next = _ => { nextCalled = true; return ValueTask.FromResult<object?>(null); };

        await filter.InvokeAsync(context, next);

        Assert.False(nextCalled);
    }
}
