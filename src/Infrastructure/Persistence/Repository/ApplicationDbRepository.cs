using Ardalis.Specification.EntityFrameworkCore;
using Boilerate.Application.Common.Persistence;
using Boilerate.Domain.Common.Contracts;
using Boilerate.Infrastructure.Persistence.Context;

namespace Boilerate.Infrastructure.Persistence.Repository;

/// <summary>
/// EF Core implementation của Repository Pattern với Ardalis.Specification.
/// </summary>
public class ApplicationDbRepository<T> : RepositoryBase<T>, IReadRepository<T>, IRepository<T>
    where T : class, IAggregateRoot
{
    public ApplicationDbRepository(ApplicationDbContext dbContext)
        : base(dbContext)
    {
    }
}
