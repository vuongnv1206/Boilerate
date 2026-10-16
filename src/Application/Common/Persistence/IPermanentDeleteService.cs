using Boilerate.Domain.Common.Contracts;

namespace Boilerate.Application.Common.Persistence;

public interface IPermanentDeleteService<T> where T : class, IAggregateRoot
{
    Task PermanentDeleteAsync(T entity, CancellationToken cancellationToken = default);
}
