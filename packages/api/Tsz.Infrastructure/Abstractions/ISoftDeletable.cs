namespace Tsz.Infrastructure.Abstractions;

public interface ISoftDeletable : IEntityBase
{
    DateTimeOffset? DeletedAt { get; }
}
