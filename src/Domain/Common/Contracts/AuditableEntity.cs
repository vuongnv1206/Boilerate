namespace Boilerate.Domain.Common.Contracts;

/// <summary>
/// Base auditable entity với Guid primary key.
/// Hỗ trợ: Created tracking, Modified tracking, Soft Delete.
/// </summary>
public abstract class AuditableEntity : BaseEntity, IAuditableEntity, ISoftDelete
{
    /// <summary>
    /// Gets or sets User ID của người tạo entity.
    /// </summary>
    public Guid CreatedBy { get; set; }

    /// <summary>
    /// Gets thời điểm tạo entity (UTC).
    /// </summary>
    public DateTime CreatedOn { get; set; }

    /// <summary>
    /// Gets or sets User ID của người modify entity lần cuối.
    /// </summary>
    public Guid LastModifiedBy { get; set; }

    /// <summary>
    /// Gets or sets thời điểm modify lần cuối (UTC).
    /// </summary>
    public DateTime? LastModifiedOn { get; set; }

    /// <summary>
    /// Gets or sets thời điểm entity bị soft delete (UTC).
    /// NULL = entity chưa bị xóa (active).
    /// Non-null = entity đã bị xóa (soft deleted).
    /// </summary>
    public DateTime? DeletedOn { get; set; }

    /// <summary>
    /// Gets or sets User ID của người soft delete entity.
    /// NULL = entity chưa bị xóa.
    /// Non-null = entity đã bị xóa bởi user này.
    /// </summary>
    public Guid? DeletedBy { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AuditableEntity"/> class.
    /// Set CreatedOn và LastModifiedOn mặc định.
    /// </summary>
    protected AuditableEntity()
    {
        CreatedOn = DateTime.UtcNow;
        LastModifiedOn = DateTime.UtcNow;
    }
}
