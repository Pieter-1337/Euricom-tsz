using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Infrastructure.Cqrs;

public sealed class Dispatcher(IServiceProvider services) : IDispatcher
{
    public Task<TResponse> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken ct = default)
    {
        var commandType = command.GetType();
        var handlerType = typeof(ICommandHandler<,>).MakeGenericType(commandType, typeof(TResponse));
        var handler = services.GetRequiredService(handlerType);

        var behaviorType = typeof(IPipelineBehavior<,>).MakeGenericType(commandType, typeof(TResponse));
        var behaviors = services.GetServices(behaviorType).ToList();

        Func<Task<TResponse>> invoke = () => InvokeHandler<TResponse>(handlerType, handler, command, ct);

        foreach (var behavior in Enumerable.Reverse(behaviors))
        {
            var captured = invoke;
            var b = behavior!;
            invoke = () => InvokeBehavior<TResponse>(behaviorType, b, command, captured, ct);
        }

        return invoke();
    }

    private static async Task<TResponse> InvokeHandler<TResponse>(
        Type handlerType, object handler, object command, CancellationToken ct)
    {
        var method = handlerType.GetMethod("HandleAsync")!;
        try
        {
            return await (Task<TResponse>)method.Invoke(handler, [command, ct])!;
        }
        catch (TargetInvocationException tie) when (tie.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(tie.InnerException).Throw();
            throw; // unreachable
        }
    }

    private static async Task<TResponse> InvokeBehavior<TResponse>(
        Type behaviorType, object behavior, object command, Func<Task<TResponse>> next, CancellationToken ct)
    {
        var method = behaviorType.GetMethod("HandleAsync")!;
        try
        {
            return await (Task<TResponse>)method.Invoke(behavior, [command, next, ct])!;
        }
        catch (TargetInvocationException tie) when (tie.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(tie.InnerException).Throw();
            throw; // unreachable
        }
    }
}
