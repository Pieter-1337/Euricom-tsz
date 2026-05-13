using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Tsz.Infrastructure.Endpoints;

public static class EndpointRouteBuilderExtensions
{
    public static RouteGroupBuilder MapApiGroup(this IEndpointRouteBuilder endpoints, string prefix)
    {
        return endpoints.MapGroup($"/api/{prefix}")
            .WithTags(prefix);
    }
}
