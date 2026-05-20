using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tsz.Infrastructure;
using Tsz.Infrastructure.Extensions;
using Tsz.Modules.Customers.Contracts;
using Tsz.Modules.Customers.Endpoints;

namespace Tsz.Modules.Customers;

public sealed class CustomersModule : IModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHandlersFromAssembly(typeof(CustomersModule).Assembly);
        services.AddValidatorsFromAssembly(typeof(CustomersModule).Assembly);
        services.AddScoped<ICustomersAccessModule, CustomersAccessModule>();
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        CustomerEndpoints.Map(app);
    }
}
