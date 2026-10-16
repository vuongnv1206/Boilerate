using System;

namespace Boilerate.Application.Auditing;

public class AuditLogExportDto
{
    public DateTime DateTime { get; set; }
    public string UserId { get; set; } = default!;
    public string Type { get; set; } = default!;
    public string TableName { get; set; } = default!;
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? AffectedColumns { get; set; }
    public string PrimaryKey { get; set; } = default!;
}
