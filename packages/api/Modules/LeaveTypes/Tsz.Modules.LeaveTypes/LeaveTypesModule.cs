using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tsz.Infrastructure;
using Tsz.Infrastructure.Extensions;
using Tsz.Modules.LeaveTypes.Contracts;
using Tsz.Modules.LeaveTypes.Endpoints;

namespace Tsz.Modules.LeaveTypes;

public sealed class LeaveTypesModule : IModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHandlersFromAssembly(typeof(LeaveTypesModule).Assembly);
        services.AddValidatorsFromAssembly(typeof(LeaveTypesModule).Assembly);
        services.AddScoped<ILeaveTypesAccessModule, LeaveTypesAccessModule>();
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        LeaveTypeEndpoints.Map(app);
    }
}
