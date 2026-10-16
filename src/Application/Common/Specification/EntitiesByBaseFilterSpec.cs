using Ardalis.Specification;
using Boilerate.Application.Common.Models;

namespace Boilerate.Application.Common.Specification;

/// <summary>
/// Base spec với search + filter (không có pagination).
/// </summary>
public class EntitiesByBaseFilterSpec<T> : Specification<T>
{
    public EntitiesByBaseFilterSpec(BaseFilter filter) =>
        Query.SearchBy(filter);
}

/// <summary>
/// Base spec với search + filter và projection.
/// </summary>
public class EntitiesByBaseFilterSpec<T, TResult> : Specification<T, TResult>
{
    public EntitiesByBaseFilterSpec(BaseFilter filter) =>
        Query.SearchBy(filter);
}
