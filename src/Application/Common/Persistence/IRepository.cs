using Ardalis.Specification;
using Boilerate.Domain.Common.Contracts;

namespace Boilerate.Application.Common.Persistence;

/// <summary>
/// Read/write repository cho aggregate roots.
/// </summary>
public interface IRepository<T> : IRepositoryBase<T>
    where T : class, IAggregateRoot
{
}

/// <summary>
/// Read-only repository cho aggregate roots.
/// </summary>
public interface IReadRepository<T> : IReadRepositoryBase<T>
    where T : class, IAggregateRoot
{
}

/// <summary>
/// Repository tự động thêm Domain Events khi Add/Update/Delete.
/// </summary>
public interface IRepositoryWithEvents<T> : IRepositoryBase<T>
    where T : class, IAggregateRoot
{
}


