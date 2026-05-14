using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace Tsz.Api.Extensions;

public static class OpenApiExtensions
{
    public static IServiceCollection AddTszOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddSchemaTransformer((schema, _, _) =>
            {
                // Remove spurious string type from numeric schemas (JsonNumberHandling artefact)
                if (schema.Type.HasValue &&
                    (schema.Type.Value & (JsonSchemaType.Integer | JsonSchemaType.Number)) != 0 &&
                    (schema.Type.Value & JsonSchemaType.String) != 0)
                {
                    schema.Type &= ~JsonSchemaType.String;
                }

                // Mark all non-nullable properties as required so TS omits the `?`
                if (schema.Properties is { Count: > 0 })
                {
                    schema.Required ??= new HashSet<string>();
                    foreach (var (name, property) in schema.Properties)
                    {
                        var isNullable = property.Type is { } t && (t & JsonSchemaType.Null) != 0;
                        if (!isNullable)
                            schema.Required.Add(name);
                    }
                }

                return Task.CompletedTask;
            });
        });

        return services;
    }
}
