using Boilerate.Domain.Common.Contracts;
using MediatR;

namespace Boilerate.Application.Common.Events;

/// <summary>
/// Wrapper class để wrap IEvent thành INotification (MediatR).
/// Giữ cho Domain layer không phụ thuộc MediatR.
/// </summary>
public class EventNotification<TEvent> : INotification
    where TEvent : IEvent
{
    public EventNotification(TEvent @event) => Event = @event;

    /// <summary>
    /// Domain event được wrap.
    /// </summary>
    public TEvent Event { get; }
}
