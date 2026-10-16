namespace Boilerate.Domain.Common.Contracts;

/// <summary>
/// Interface cho entities có audit trail (Created/Modified tracking).
/// </summary>
public interface IAuditableEntity
{
    /// <summary>
    /// Gets or sets User ID của người tạo entity.
    /// </summary>
    Guid CreatedBy { get; set; }

    /// <summary>
    /// Gets thời điểm tạo entity (UTC).
    /// </summary>
    DateTime CreatedOn { get; set; }

    /// <summary>
    /// Gets or sets User ID của người modify entity lần cuối.
    /// </summary>
    Guid LastModifiedBy { get; set; }

    /// <summary>
    /// Gets or sets thời điểm modify lần cuối (UTC).
    /// </summary>
    DateTime? LastModifiedOn { get; set; }
}
