using System.ComponentModel.DataAnnotations.Schema;
using MassTransit;

namespace Boilerate.Domain.Common.Contracts;

/// <summary>
/// Base entity with generic Id type
/// </summary>
public abstract class BaseEntity<TId> : IEntity<TId>
{
    public TId Id { get; protected set; } = default!;

    [NotMapped]
    public List<DomainEvent> DomainEvents { get; } = new();
}

/// <summary>
/// Base entity with Guid Id (most common case)
/// </summary>
public abstract class BaseEntity : BaseEntity<Guid>
{
    protected BaseEntity() => Id = NewId.Next().ToGuid();
}
