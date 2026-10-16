namespace Boilerate.Domain.Common.Contracts;

/// <summary>
/// Marker interface for aggregate root entities.
/// Repositories should only work with aggregate roots, not their children.
/// </summary>
public interface IAggregateRoot : IEntity
{
}
