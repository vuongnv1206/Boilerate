namespace Boilerate.Domain.Common.Contracts;

/// <summary>
/// Base interface for all entities
/// </summary>
public interface IEntity
{
    List<DomainEvent> DomainEvents { get; }
}

/// <summary>
/// Base interface for entities with typed Id
/// </summary>
public interface IEntity<TId> : IEntity
{
    TId Id { get; }
}
