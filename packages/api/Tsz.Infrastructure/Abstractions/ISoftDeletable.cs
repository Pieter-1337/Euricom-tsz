namespace Tsz.Infrastructure.Abstractions;

public interface ISoftDeletable
{
    DateTimeOffset? DeletedAt { get; }
}
