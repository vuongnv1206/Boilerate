using Boilerate.Application.Common.Models;

namespace Boilerate.Application.Auditing;

/// <summary>
/// Request to get current user's audit logs.
/// </summary>
public class GetMyAuditLogsRequest : PaginationFilter
{
    /// <summary>
    /// Filter by table name (optional).
    /// </summary>
    public string? TableName { get; set; }

    /// <summary>
    /// Filter by trail type (optional).
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Filter by date from (optional).
    /// </summary>
    public DateTime? FromDate { get; set; }

    /// <summary>
    /// Filter by date to (optional).
    /// </summary>
    public DateTime? ToDate { get; set; }
}
