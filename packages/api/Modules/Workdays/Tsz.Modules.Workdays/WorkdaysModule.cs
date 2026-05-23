using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tsz.Infrastructure;
using Tsz.Infrastructure.Extensions;
using Tsz.Modules.Workdays.Contracts;

namespace Tsz.Modules.Workdays;

public sealed class WorkdaysModule : IModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHandlersFromAssembly(typeof(WorkdaysModule).Assembly);
        services.AddValidatorsFromAssembly(typeof(WorkdaysModule).Assembly);
        services.AddScoped<IWorkdaysAccessModule, WorkdaysAccessModule>();
    }

    public void MapEndpoints(IEndpointRouteBuilder app) { }
}
