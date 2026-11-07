namespace Boilerate.Application.Common.Persistence;

public interface ICommerceUnitOfWork
{
    Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken ct);
}
