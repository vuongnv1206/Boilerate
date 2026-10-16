namespace Boilerate.Domain.Auditing;

/// <summary>
/// Type of audit trail entry.
/// Represents the type of change that occurred to an entity.
/// </summary>
public enum TrailType : byte
{
    /// <summary>
    /// Entity was created (INSERT operation)
    /// </summary>
    Create = 1,

    /// <summary>
    /// Entity was updated (UPDATE operation)
    /// </summary>
    Update = 2,

    /// <summary>
    /// Entity was deleted (DELETE or soft delete operation)
    /// </summary>
    Delete = 3
}
