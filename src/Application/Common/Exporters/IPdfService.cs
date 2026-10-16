using Boilerate.Application.Common.Interfaces;
namespace Boilerate.Application.Common.Exporters;
public interface IPdfService : ITransientService
{
    Task<byte[]> CreateInvoicePdfAsync(InvoiceData data, PdfOptions? options = null, CancellationToken cancellationToken = default);
    Task<byte[]> CreateReportPdfAsync(ReportData data, PdfOptions? options = null, CancellationToken cancellationToken = default);
}
