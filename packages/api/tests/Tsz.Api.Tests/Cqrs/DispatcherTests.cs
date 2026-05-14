using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Cqrs;
using Tsz.Infrastructure.Extensions;

namespace Tsz.Api.Tests.Cqrs;

public class DispatcherTests
{
    private sealed record PingCommand(string Payload) : ICommand<string>;

    private sealed class PingHandler : ICommandHandler<PingCommand, string>
    {
        public Task<string> HandleAsync(PingCommand command, CancellationToken ct = default)
            => Task.FromResult($"pong:{command.Payload}");
    }

    private sealed class ThrowingHandler : ICommandHandler<PingCommand, string>
    {
        public Task<string> HandleAsync(PingCommand command, CancellationToken ct = default)
            => throw new InvalidOperationException("handler-boom");
    }

    private sealed class OrderCapturingBehavior(string name, List<string> log)
        : IPipelineBehavior<PingCommand, string>
    {
        public async Task<string> HandleAsync(PingCommand command, Func<Task<string>> next, CancellationToken ct)
        {
            log.Add($"{name}:before");
            var result = await next();
            log.Add($"{name}:after");
            return result;
        }
    }

    private static IDispatcher BuildDispatcher(
        ICommandHandler<PingCommand, string> handler,
        IEnumerable<IPipelineBehavior<PingCommand, string>>? behaviors = null)
    {
        var services = new ServiceCollection();
        services.AddScoped<ICommandHandler<PingCommand, string>>(_ => handler);
        services.AddDispatcher();

        foreach (var b in behaviors ?? [])
        {
            var captured = b;
            services.AddScoped<IPipelineBehavior<PingCommand, string>>(_ => captured);
        }

        return services.BuildServiceProvider().GetRequiredService<IDispatcher>();
    }

    [Fact]
    public async Task SendAsync_ResolvesHandler_AndReturnsResult()
    {
        var dispatcher = BuildDispatcher(new PingHandler());

        var result = await dispatcher.SendAsync(new PingCommand("hello"));

        result.ShouldBe("pong:hello");
    }

    [Fact]
    public async Task SendAsync_NoBehaviors_HandlerRunsDirectly()
    {
        var log = new List<string>();
        var dispatcher = BuildDispatcher(new PingHandler());

        var result = await dispatcher.SendAsync(new PingCommand("x"));

        result.ShouldBe("pong:x");
        log.ShouldBeEmpty();
    }

    [Fact]
    public async Task SendAsync_MultipleBehaviors_RunInRegistrationOrder()
    {
        var log = new List<string>();
        var b1 = new OrderCapturingBehavior("B1", log);
        var b2 = new OrderCapturingBehavior("B2", log);

        var dispatcher = BuildDispatcher(new PingHandler(), [b1, b2]);

        await dispatcher.SendAsync(new PingCommand("order"));

        log.ShouldBe(["B1:before", "B2:before", "B2:after", "B1:after"]);
    }

    [Fact]
    public async Task SendAsync_HandlerThrows_PropagatesException()
    {
        var dispatcher = BuildDispatcher(new ThrowingHandler());

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => dispatcher.SendAsync(new PingCommand("boom")));
        ex.Message.ShouldBe("handler-boom");
    }
}
