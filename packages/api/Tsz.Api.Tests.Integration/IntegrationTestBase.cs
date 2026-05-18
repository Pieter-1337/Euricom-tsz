using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Tsz.Api.Tests.Integration.TestAuth;
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Tests.Integration;

public abstract class IntegrationTestBase : IClassFixture<TestWebApplicationFactory>
{
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    protected TestWebApplicationFactory Factory { get; }
    protected HttpClient Client { get; }

    protected IntegrationTestBase(TestWebApplicationFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }

    protected async Task WithUowAsync(Func<IUnitOfWork, Task> action)
    {
        using var scope = Factory.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await action(uow);
    }

    protected async Task<T> WithUowAsync<T>(Func<IUnitOfWork, Task<T>> action)
    {
        using var scope = Factory.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        return await action(uow);
    }
}
