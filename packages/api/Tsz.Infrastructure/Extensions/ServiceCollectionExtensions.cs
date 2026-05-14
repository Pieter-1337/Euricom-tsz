using System.Reflection;
using Tsz.Infrastructure.Abstractions;
using Tsz.Infrastructure.Cqrs;
using Tsz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Tsz.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork<TContext>>();
        return services;
    }

    public static IServiceCollection AddDispatcher(this IServiceCollection services)
    {
        services.AddScoped<IDispatcher, Dispatcher>();
        return services;
    }

    public static IServiceCollection AddHandlersFromAssembly(this IServiceCollection services, Assembly assembly)
    {
        var implementations = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false });

        foreach (var impl in implementations)
        {
            var handlerInterfaces = impl.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>));

            foreach (var handlerInterface in handlerInterfaces)
                services.AddScoped(handlerInterface, impl);
        }

        return services;
    }
}
