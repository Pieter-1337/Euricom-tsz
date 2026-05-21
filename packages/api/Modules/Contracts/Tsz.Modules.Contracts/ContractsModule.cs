using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tsz.Infrastructure;
using Tsz.Infrastructure.Extensions;
using Tsz.Modules.Contracts.Contracts;
using Tsz.Modules.Contracts.Endpoints;

namespace Tsz.Modules.Contracts;

public sealed class ContractsModule : IModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHandlersFromAssembly(typeof(ContractsModule).Assembly);
        services.AddValidatorsFromAssembly(typeof(ContractsModule).Assembly);
        services.AddScoped<IContractsAccessModule, ContractsAccessModule>();
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        ContractEndpoints.Map(app);
    }
}
