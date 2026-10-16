using Boilerate.Domain.Common.Contracts;

namespace Boilerate.Domain.Auditing;

/// <summary>
/// Audit trail entity - lưu trữ tất cả thay đổi trong hệ thống.
/// Mỗi record represent một operation (Create/Update/Delete) trên một entity.
/// </summary>
public class Trail : BaseEntity, IAggregateRoot
{
    /// <summary>
    /// User ID của người thực hiện action (from ICurrentUser)
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Type of operation (Create/Update/Delete)
    /// </summary>
    public TrailType Type { get; set; }

    /// <summary>
    /// Table name của entity bị modify (e.g., "Products", "ApplicationUser")
    /// </summary>
    public string TableName { get; set; } = default!;

    /// <summary>
    /// Thời điểm thay đổi (UTC)
    /// </summary>
    public DateTime DateTime { get; set; }

    /// <summary>
    /// Old values (before change) - serialized as JSON
    /// NULL for Create operations
    /// </summary>
    public string? OldValues { get; set; }

    /// <summary>
    /// New values (after change) - serialized as JSON
    /// NULL for Delete operations
    /// </summary>
    public string? NewValues { get; set; }

    /// <summary>
    /// Affected columns (changed properties) - comma-separated
    /// e.g., "FirstName,Email,PhoneNumber"
    /// </summary>
    public string? AffectedColumns { get; set; }

    /// <summary>
    /// Primary key của entity bị modify - serialized as JSON
    /// Hỗ trợ composite keys: { "Id": "guid", "TenantId": "guid" }
    /// </summary>
    public string PrimaryKey { get; set; } = default!;

    /// <summary>
    /// Constructor - Initialize DateTime
    /// </summary>
    public Trail()
    {
        DateTime = DateTime.UtcNow;
    }
}
