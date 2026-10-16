namespace Boilerate.Application.Auditing;

/// <summary>
/// DTO for audit trail entry.
/// </summary>
public class AuditDto
{
    /// <summary>
    /// Audit trail ID.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// User ID performed the action.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Type of operation (Create/Update/Delete).
    /// </summary>
    public string Type { get; set; } = default!;

    /// <summary>
    /// Table name (entity type).
    /// </summary>
    public string TableName { get; set; } = default!;

    /// <summary>
    /// When the change occurred (UTC).
    /// </summary>
    public DateTime DateTime { get; set; }

    /// <summary>
    /// Old values (JSON string).
    /// </summary>
    public string? OldValues { get; set; }

    /// <summary>
    /// New values (JSON string).
    /// </summary>
    public string? NewValues { get; set; }

    /// <summary>
    /// Affected columns (comma-separated).
    /// </summary>
    public string? AffectedColumns { get; set; }

    /// <summary>
    /// Primary key (JSON string).
    /// </summary>
    public string PrimaryKey { get; set; } = default!;
}
