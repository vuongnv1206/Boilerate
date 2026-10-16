namespace Boilerate.Domain.Common.Contracts;

/// <summary>
/// Marker interface cho entities hỗ trợ soft delete.
/// Entities implement interface này sẽ:
/// - Được đánh dấu DeletedOn/DeletedBy thay vì xóa vật lý.
/// - Tự động bị exclude khỏi queries (via global query filter).
/// - Có thể restore bằng cách set DeletedOn = null.
/// </summary>
public interface ISoftDelete
{
    /// <summary>
    /// Gets or sets thời điểm entity bị xóa (UTC).
    /// NULL = entity chưa bị xóa (active).
    /// Non-null = entity đã bị xóa (soft deleted).
    /// </summary>
    DateTime? DeletedOn { get; set; }

    /// <summary>
    /// Gets or sets User ID của người xóa entity.
    /// NULL = entity chưa bị xóa.
    /// Non-null = entity đã bị xóa bởi user này.
    /// </summary>
    Guid? DeletedBy { get; set; }
}
