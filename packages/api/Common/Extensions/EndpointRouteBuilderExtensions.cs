namespace Api.Common.Extensions;

public static class EndpointRouteBuilderExtensions
{
    public static RouteGroupBuilder MapApiGroup(this IEndpointRouteBuilder endpoints, string prefix)
    {
        return endpoints.MapGroup($"/api/{prefix}")
            .WithTags(prefix);
    }
}
