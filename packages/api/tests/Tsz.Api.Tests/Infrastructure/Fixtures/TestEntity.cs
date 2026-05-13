using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Tests.Infrastructure.Fixtures;

public class TestEntity : IEntityBase
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public static TestEntity Create(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
    };
}
