using Tsz.Infrastructure.Validation;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Tsz.Api.Tests.Common.Filters;

public class ValidationFilterTests
{
    private sealed record TestModel(string Name, int Count);

    private sealed class TestModelValidator : AbstractValidator<TestModel>
    {
        public TestModelValidator()
        {
            RuleFor(x => x.Name).NotEmpty();
            RuleFor(x => x.Count).GreaterThanOrEqualTo(0);
        }
    }

    private sealed class FakeFilterContext(IServiceProvider services, params object?[] args)
        : EndpointFilterInvocationContext
    {
        public override HttpContext HttpContext { get; } = new DefaultHttpContext { RequestServices = services };
        public override IList<object?> Arguments { get; } = [.. args];
        public override T GetArgument<T>(int index) => (T)args[index]!;
    }

    private static IServiceProvider BuildServices(bool withValidator)
    {
        var services = new ServiceCollection();
        if (withValidator)
            services.AddScoped<IValidator<TestModel>, TestModelValidator>();
        return services.BuildServiceProvider();
    }

    private static EndpointFilterDelegate NextReturning(object? value) =>
        _ => ValueTask.FromResult(value);

    private static int GetResultStatus(object? result) =>
        ((IStatusCodeHttpResult)result!).StatusCode ?? 200;

    [Fact]
    public async Task InvokeAsync_MissingArgument_ReturnsBadRequest()
    {
        var filter = new ValidationFilter<TestModel>();
        var context = new FakeFilterContext(BuildServices(withValidator: true));

        var result = await filter.InvokeAsync(context, NextReturning("sentinel"));

        GetResultStatus(result).ShouldBe(400);
    }

    [Fact]
    public async Task InvokeAsync_NoValidatorRegistered_CallsNext()
    {
        var filter = new ValidationFilter<TestModel>();
        var model = new TestModel("anything", -42);
        var context = new FakeFilterContext(BuildServices(withValidator: false), model);
        var sentinel = new object();

        var result = await filter.InvokeAsync(context, NextReturning(sentinel));

        result.ShouldBeSameAs(sentinel);
    }

    [Fact]
    public async Task InvokeAsync_ValidModel_CallsNext()
    {
        var filter = new ValidationFilter<TestModel>();
        var model = new TestModel("ok", 3);
        var context = new FakeFilterContext(BuildServices(withValidator: true), model);
        var sentinel = new object();

        var result = await filter.InvokeAsync(context, NextReturning(sentinel));

        result.ShouldBeSameAs(sentinel);
    }

    [Fact]
    public async Task InvokeAsync_InvalidModel_ReturnsValidationProblem()
    {
        var filter = new ValidationFilter<TestModel>();
        var model = new TestModel("", -1);
        var context = new FakeFilterContext(BuildServices(withValidator: true), model);

        var result = await filter.InvokeAsync(context, NextReturning("sentinel"));

        GetResultStatus(result).ShouldBe(400);
    }

    [Fact]
    public async Task InvokeAsync_InvalidModel_DoesNotCallNext()
    {
        var filter = new ValidationFilter<TestModel>();
        var model = new TestModel("", -1);
        var context = new FakeFilterContext(BuildServices(withValidator: true), model);
        var nextCalled = false;
        EndpointFilterDelegate next = _ => { nextCalled = true; return ValueTask.FromResult<object?>(null); };

        await filter.InvokeAsync(context, next);

        nextCalled.ShouldBeFalse();
    }
}
