using Boilerate.Domain.Common.Contracts;
using MediatR;

namespace Boilerate.Application.Common.Events;

/// <summary>
/// Interface cho event notification handlers (shorthand).
/// </summary>
public interface IEventNotificationHandler<TEvent> : INotificationHandler<EventNotification<TEvent>>
    where TEvent : IEvent
{
}

/// <summary>
/// Abstract base class cho event notification handlers.
/// Auto unwrap EventNotification để handlers chỉ cần handle domain event.
/// </summary>
public abstract class EventNotificationHandler<TEvent> : INotificationHandler<EventNotification<TEvent>>
    where TEvent : IEvent
{
    /// <summary>
    /// Handle EventNotification (wrapper) - auto called bởi MediatR.
    /// </summary>
    public Task Handle(EventNotification<TEvent> notification, CancellationToken cancellationToken) =>
        Handle(notification.Event, cancellationToken);

    /// <summary>
    /// Handle domain event (phải implement trong derived class).
    /// </summary>
    public abstract Task Handle(TEvent @event, CancellationToken cancellationToken);
}
