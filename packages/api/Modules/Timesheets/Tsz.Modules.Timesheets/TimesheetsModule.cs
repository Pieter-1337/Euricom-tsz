using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tsz.Infrastructure;
using Tsz.Infrastructure.Extensions;
using Tsz.Modules.Timesheets.Endpoints;

namespace Tsz.Modules.Timesheets;

public sealed class TimesheetsModule : IModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHandlersFromAssembly(typeof(TimesheetsModule).Assembly);
        services.AddValidatorsFromAssembly(typeof(TimesheetsModule).Assembly);
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        TimesheetEndpoints.Map(app);
    }
}
