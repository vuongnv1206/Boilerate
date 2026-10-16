namespace Boilerate.Domain.Common.Contracts;

/// <summary>
/// Base class for all domain events
/// </summary>
public abstract class DomainEvent : IEvent
{
    /// <summary>
    /// When this event was triggered
    /// </summary>
    public DateTime TriggeredOn { get; protected set; } = DateTime.UtcNow;
}
