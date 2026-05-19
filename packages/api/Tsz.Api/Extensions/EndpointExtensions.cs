using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Scalar.AspNetCore;
using Tsz.Api.Modules.Customers;
using Tsz.Api.Modules.Users;

namespace Tsz.Api.Extensions;

public static class EndpointExtensions
{
    public static IEndpointRouteBuilder MapTszEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapOpenApi("/openapi/{documentName}.json").AllowAnonymous();
        app.MapScalarApiReference("/openapi", options =>
        {
            options.WithOpenApiRoutePattern("/openapi/{documentName}.json");
        }).AllowAnonymous();

        app.MapGet("/", () => new
        {
            name = "Tsz API",
            version = Assembly.GetExecutingAssembly().GetName().Version?.ToString()
        }).AllowAnonymous();

        UserEndpoints.Map(app);
        CustomerEndpoints.Map(app);

        return app;
    }
}
