using Boilerate.Application.Common.Models;
using MediatR;
using System;

namespace Boilerate.Application.Auditing;

/// <summary>
/// Request to export audit logs to Excel
/// </summary>
public class ExportAuditLogsRequest : BaseFilter, IRequest<byte[]>
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? UserId { get; set; }
    public string? TableName { get; set; }
}
