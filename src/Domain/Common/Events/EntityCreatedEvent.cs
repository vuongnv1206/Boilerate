using Boilerate.Domain.Common.Contracts;

namespace Boilerate.Domain.Common.Events;

public static class EntityCreatedEvent
{
    public static EntityCreatedEvent<TEntity> WithEntity<TEntity>(TEntity entity)
        where TEntity : IEntity
        => new(entity);
}

public class EntityCreatedEvent<TEntity> : DomainEvent
    where TEntity : IEntity
{
    public TEntity Entity { get; }
    
    internal EntityCreatedEvent(TEntity entity) => Entity = entity;
}
