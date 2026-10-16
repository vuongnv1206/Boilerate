using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Models;

namespace Boilerate.Application.Auditing;

/// <summary>
/// Service interface for querying audit trails.
/// </summary>
public interface IAuditService : ITransientService
{
    /// <summary>
    /// Get audit logs for current user (my audit history).
    /// </summary>
    Task<PaginationResponse<AuditDto>> GetMyAuditLogsAsync(
        GetMyAuditLogsRequest request,
        CancellationToken cancellationToken = default);
}
