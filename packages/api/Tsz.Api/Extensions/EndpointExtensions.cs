using System.Reflection;
using Scalar.AspNetCore;

namespace Tsz.Api.Extensions;

public static class EndpointExtensions
{
    public static IEndpointRouteBuilder MapTszOpenApi(this IEndpointRouteBuilder app)
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

        return app;
    }
}
