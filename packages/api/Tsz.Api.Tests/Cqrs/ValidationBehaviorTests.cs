using FluentValidation;
using FluentValidation.Results;
using Shouldly;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Cqrs;

namespace Tsz.Api.Tests.Cqrs;

public class ValidationBehaviorTests
{
    private sealed record DummyCommand(string Value) : ICommand<string>;

    private static readonly Func<Task<string>> NextReturnsOk =
        () => Task.FromResult("ok");

    private sealed class PassingValidator : AbstractValidator<DummyCommand>
    {
        public PassingValidator() => RuleFor(x => x.Value).NotEmpty();
    }

    private sealed class FailingValidator : AbstractValidator<DummyCommand>
    {
        public FailingValidator()
        {
            RuleFor(x => x.Value)
                .Must(_ => false)
                .WithMessage("always fails")
                .WithErrorCode("ERR_ALWAYS_FAILS");
        }
    }

    private sealed class AnotherFailingValidator : AbstractValidator<DummyCommand>
    {
        public AnotherFailingValidator()
        {
            RuleFor(x => x.Value)
                .Must(_ => false)
                .WithMessage("second failure")
                .WithErrorCode("ERR_SECOND");
        }
    }

    [Fact]
    public async Task NoValidators_PassesThrough()
    {
        var behavior = new ValidationBehavior<DummyCommand, string>([]);

        var result = await behavior.HandleAsync(new DummyCommand("x"), NextReturnsOk, default);

        result.ShouldBe("ok");
    }

    [Fact]
    public async Task PassingValidator_PassesThrough()
    {
        var behavior = new ValidationBehavior<DummyCommand, string>([new PassingValidator()]);

        var result = await behavior.HandleAsync(new DummyCommand("valid"), NextReturnsOk, default);

        result.ShouldBe("ok");
    }

    [Fact]
    public async Task FailingValidator_ThrowsValidationException()
    {
        var behavior = new ValidationBehavior<DummyCommand, string>([new FailingValidator()]);

        var ex = await Should.ThrowAsync<FluentValidation.ValidationException>(
            () => behavior.HandleAsync(new DummyCommand("x"), NextReturnsOk, default));

        ex.Errors.ShouldContain(f => f.ErrorCode == "ERR_ALWAYS_FAILS");
    }

    [Fact]
    public async Task MultipleValidators_AggregatesAllFailures()
    {
        var behavior = new ValidationBehavior<DummyCommand, string>(
            [new FailingValidator(), new AnotherFailingValidator()]);

        var ex = await Should.ThrowAsync<FluentValidation.ValidationException>(
            () => behavior.HandleAsync(new DummyCommand("x"), NextReturnsOk, default));

        ex.Errors.ShouldContain(f => f.ErrorCode == "ERR_ALWAYS_FAILS");
        ex.Errors.ShouldContain(f => f.ErrorCode == "ERR_SECOND");
    }

    [Fact]
    public async Task FailingValidator_DoesNotCallNext()
    {
        var nextCalled = false;
        Func<Task<string>> next = () => { nextCalled = true; return Task.FromResult("ok"); };
        var behavior = new ValidationBehavior<DummyCommand, string>([new FailingValidator()]);

        await Should.ThrowAsync<FluentValidation.ValidationException>(
            () => behavior.HandleAsync(new DummyCommand("x"), next, default));

        nextCalled.ShouldBeFalse();
    }
}
