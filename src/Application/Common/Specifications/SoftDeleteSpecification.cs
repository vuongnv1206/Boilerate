using Ardalis.Specification;
using Boilerate.Domain.Common.Contracts;

namespace Boilerate.Application.Common.Specifications;

/// <summary>
/// Specification base cho soft delete queries.
/// Provides methods để include/exclude deleted entities.
/// </summary>
/// <typeparam name="T">Entity type implementing ISoftDelete.</typeparam>
public abstract class SoftDeleteSpecification<T> : Specification<T>
    where T : class, ISoftDelete
{
    /// <summary>
    /// Include deleted entities trong query.
    /// Disables global query filter.
    /// </summary>
    protected void IncludeDeleted()
    {
        Query.IgnoreQueryFilters();
    }

    /// <summary>
    /// Query chỉ deleted entities.
    /// Disables global filter và add explicit filter: DeletedOn != null.
    /// </summary>
    protected void OnlyDeleted()
    {
        Query.IgnoreQueryFilters().Where(e => e.DeletedOn != null);
    }
}
