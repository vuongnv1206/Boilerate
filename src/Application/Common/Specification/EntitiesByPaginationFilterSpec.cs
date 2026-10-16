using Boilerate.Application.Common.Models;

namespace Boilerate.Application.Common.Specification;

/// <summary>
/// Base spec với search + filter + pagination.
/// </summary>
public class EntitiesByPaginationFilterSpec<T> : EntitiesByBaseFilterSpec<T>
{
    public EntitiesByPaginationFilterSpec(PaginationFilter? filter)
        : base(filter ?? new PaginationFilter())
    {
        Query.PaginateBy(filter ?? new PaginationFilter());
    }
}

/// <summary>
/// Base spec với search + filter + pagination và projection.
/// </summary>
public class EntitiesByPaginationFilterSpec<T, TResult> : EntitiesByBaseFilterSpec<T, TResult>
{
    public EntitiesByPaginationFilterSpec(PaginationFilter? filter)
        : base(filter ?? new PaginationFilter())
    {
        Query.PaginateBy(filter ?? new PaginationFilter());
    }
}
