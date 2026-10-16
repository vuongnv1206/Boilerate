using Boilerate.Domain.Common.Contracts;

namespace Boilerate.Application.Common.Extensions;

/// <summary>
/// Extension methods cho soft delete operations.
/// </summary>
public static class SoftDeleteExtensions
{
    /// <summary>
    /// Restore deleted entity bằng cách set DeletedOn/DeletedBy = null.
    /// </summary>
    /// <typeparam name="T">Entity type implementing ISoftDelete.</typeparam>
    /// <param name="entity">Entity to restore.</param>
    public static void Restore<T>(this T entity)
        where T : ISoftDelete
    {
        entity.DeletedOn = null;
        entity.DeletedBy = null;
    }

    /// <summary>
    /// Check if entity đã bị soft delete.
    /// </summary>
    /// <typeparam name="T">Entity type implementing ISoftDelete.</typeparam>
    /// <param name="entity">Entity to check.</param>
    /// <returns>True if entity is soft deleted (DeletedOn != null).</returns>
    public static bool IsDeleted<T>(this T entity)
        where T : ISoftDelete
    {
        return entity.DeletedOn.HasValue;
    }

    /// <summary>
    /// Soft delete entity manually (normally handled by SaveChangesAsync).
    /// </summary>
    /// <typeparam name="T">Entity type implementing ISoftDelete.</typeparam>
    /// <param name="entity">Entity to soft delete.</param>
    /// <param name="userId">User performing the delete.</param>
    public static void SoftDelete<T>(this T entity, Guid userId)
        where T : ISoftDelete
    {
        entity.DeletedOn = DateTime.UtcNow;
        entity.DeletedBy = userId;
    }
}
