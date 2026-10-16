using Boilerate.Application.Common.Interfaces;
using Boilerate.Domain.Common.Contracts;

namespace Boilerate.Application.Common.Events;

/// <summary>
/// Interface để publish domain events.
/// Implementation sẽ dùng MediatR để dispatch events đến handlers.
/// </summary>
public interface IEventPublisher : ITransientService
{
    /// <summary>
    /// Publish domain event.
    /// </summary>
    Task PublishAsync(IEvent @event);
}
