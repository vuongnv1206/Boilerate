using Boilerate.Application.Common.Events;
using Boilerate.Domain.Common.Contracts;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Boilerate.Infrastructure.Common.Events;

/// <summary>
/// Implementation của IEventPublisher sử dụng MediatR.
/// </summary>
public class EventPublisher : IEventPublisher
{
    private readonly ILogger<EventPublisher> _logger;
    private readonly IPublisher _mediator;

    public EventPublisher(ILogger<EventPublisher> logger, IPublisher mediator) =>
        (_logger, _mediator) = (logger, mediator);

    /// <summary>
    /// Publish domain event qua MediatR.
    /// </summary>
    public Task PublishAsync(IEvent @event)
    {
        // Log event type để tracking
        _logger.LogInformation("Publishing Event: {EventType}", @event.GetType().Name);

        // Wrap event thành EventNotification và publish qua MediatR
        return _mediator.Publish(CreateEventNotification(@event));
    }

    /// <summary>
    /// Create EventNotification&lt;TEvent&gt; từ IEvent bằng reflection.
    /// Vì runtime type, không thể dùng generic compile-time.
    /// </summary>
    private static INotification CreateEventNotification(IEvent @event)
    {
        // Step 1: Lấy runtime type của event (ví dụ: ProductCreatedEvent)
        var eventType = @event.GetType();

        // Step 2: Tạo generic type EventNotification<ProductCreatedEvent>
        var notificationType = typeof(EventNotification<>).MakeGenericType(eventType);

        // Step 3: Create instance: new EventNotification<ProductCreatedEvent>(event)
        var instance = Activator.CreateInstance(notificationType, @event);

        // Step 4: Cast về INotification
        return (INotification)instance!;
    }
}
