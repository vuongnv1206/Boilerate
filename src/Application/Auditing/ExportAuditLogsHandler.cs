using Boilerate.Application.Common.Exporters;
using Boilerate.Application.Common.Persistence;
using Boilerate.Domain.Auditing;
using MediatR;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Boilerate.Application.Auditing;

public class ExportAuditLogsHandler : IRequestHandler<ExportAuditLogsRequest, byte[]>
{
    private readonly IReadRepository<Trail> _repository;
    private readonly IExcelWriter _excelWriter;

    public ExportAuditLogsHandler(
        IReadRepository<Trail> repository,
        IExcelWriter excelWriter)
    {
        _repository = repository;
        _excelWriter = excelWriter;
    }

    public async Task<byte[]> Handle(
        ExportAuditLogsRequest request,
        CancellationToken cancellationToken)
    {
        // Build specification
        var spec = new AuditLogsExportSpec(request);

        // Get data
        var trails = await _repository.ListAsync(spec, cancellationToken);

        // Map to export DTO
        var exportData = trails.Select(t => new AuditLogExportDto
        {
            DateTime = t.DateTime,
            UserId = t.UserId.ToString(),
            Type = t.Type.ToString(),
            TableName = t.TableName,
            OldValues = t.OldValues,
            NewValues = t.NewValues,
            AffectedColumns = t.AffectedColumns,
            PrimaryKey = t.PrimaryKey
        }).ToList();

        // Export to Excel
        return await _excelWriter.WriteAsync(
            data: exportData,
            sheetName: "Audit Logs",
            headers: new List<string>
            {
                "Date Time",
                "User ID",
                "Action",
                "Table",
                "Old Values",
                "New Values",
                "Changed Columns",
                "Record ID"
            },
            title: "HỆ THỐNG TRUY VẾT DỮ LIỆU (AUDIT LOGS)",
            cancellationToken: cancellationToken);
    }
}
