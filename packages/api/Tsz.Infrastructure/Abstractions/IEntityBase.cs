namespace Tsz.Infrastructure.Abstractions;

public interface IEntityBase : ISoftDeletable
{
    Guid Id { get; set; }
}
