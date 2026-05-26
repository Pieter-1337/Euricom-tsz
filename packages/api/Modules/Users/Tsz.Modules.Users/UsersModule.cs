using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tsz.Infrastructure;
using Tsz.Infrastructure.Auth;
using Tsz.Infrastructure.Extensions;
using Tsz.Modules.Users.Auth;
using Tsz.Modules.Users.Contracts;
using Tsz.Modules.Users.Endpoints;
using Tsz.Modules.Users.Seeding;

namespace Tsz.Modules.Users;

public sealed class UsersModule : IModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHandlersFromAssembly(typeof(UsersModule).Assembly);
        services.AddValidatorsFromAssembly(typeof(UsersModule).Assembly);
        services.AddScoped<CurrentUserResolver>();
        services.AddScoped<ICurrentUserResolver>(sp => sp.GetRequiredService<CurrentUserResolver>());
        services.AddScoped<ICurrentUserAccount>(sp => sp.GetRequiredService<CurrentUserResolver>());
        services.AddScoped<IUsersAccessModule, UsersAccessModule>();
        services.AddScoped<IDataScopeAccessor, DataScopeAccessor>();
        services.AddScoped<IAuthorizationHandler, RequireAdminAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, RequireAdminOrAnyClientManagerAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, RequireAdminOrSelfAuthorizationHandler>();
        services.AddScoped<UserSeeder>();
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        UserEndpoints.Map(app);
    }
}
