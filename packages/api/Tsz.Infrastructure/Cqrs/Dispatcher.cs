using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Infrastructure.Cqrs;

public sealed class Dispatcher(IServiceProvider services) : IDispatcher
{
    public Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
    {
        var requestType = request.GetType();
        var handlerType = typeof(IRequestHandler<,>).MakeGenericType(requestType, typeof(TResponse));
        var handler = services.GetRequiredService(handlerType);

        var behaviorType = typeof(IPipelineBehavior<,>).MakeGenericType(requestType, typeof(TResponse));
        var behaviors = services.GetServices(behaviorType).ToList();

        Func<Task<TResponse>> invoke = () => InvokeHandler<TResponse>(handlerType, handler, request, ct);

        foreach (var behavior in Enumerable.Reverse(behaviors))
        {
            var captured = invoke;
            var b = behavior!;
            invoke = () => InvokeBehavior<TResponse>(behaviorType, b, request, captured, ct);
        }

        return invoke();
    }

    private static async Task<TResponse> InvokeHandler<TResponse>(
        Type handlerType, object handler, object request, CancellationToken ct)
    {
        var method = handlerType.GetMethod("HandleAsync")!;
        try
        {
            return await (Task<TResponse>)method.Invoke(handler, [request, ct])!;
        }
        catch (TargetInvocationException tie) when (tie.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(tie.InnerException).Throw();
            throw;
        }
    }

    private static async Task<TResponse> InvokeBehavior<TResponse>(
        Type behaviorType, object behavior, object request, Func<Task<TResponse>> next, CancellationToken ct)
    {
        var method = behaviorType.GetMethod("HandleAsync")!;
        try
        {
            return await (Task<TResponse>)method.Invoke(behavior, [request, next, ct])!;
        }
        catch (TargetInvocationException tie) when (tie.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(tie.InnerException).Throw();
            throw;
        }
    }
}
