using Boilerate.Application.Common.Exporters;
using Boilerate.Application.Common.Persistence;
using Boilerate.Domain.Catalog;
using MediatR;

namespace Boilerate.Application.Catalog.Products;

public class ExportProductsRequest : IRequest<Stream>
{
    public string ExportType { get; set; } = "excel"; // "excel" or "pdf"
}

public class ExportProductsRequestHandler : IRequestHandler<ExportProductsRequest, Stream>
{
    private readonly IReadRepository<Product> _repository;
    private readonly IExcelWriter _excelWriter;
    private readonly IPdfService _pdfService;

    public ExportProductsRequestHandler(
        IReadRepository<Product> repository, 
        IExcelWriter excelWriter,
        IPdfService pdfService)
    {
        _repository = repository;
        _excelWriter = excelWriter;
        _pdfService = pdfService;
    }

    public async Task<Stream> Handle(ExportProductsRequest request, CancellationToken cancellationToken)
    {
        // Lấy tất cả Products (bao gồm Category để hiển thị tên)
        var products = await _repository.ListAsync(new AllProductsIncludingDeletedSpec(), cancellationToken);

        if (request.ExportType?.ToLower() == "pdf")
        {
            var reportData = new ReportData
            {
                Title = "Danh sách Sản phẩm",
                Subtitle = $"Xuất ngày: {DateTime.Now:dd/MM/yyyy HH:mm}",
                Summary = $"Tổng cộng có {products.Count} sản phẩm trong hệ thống.",
                Sections = new List<ReportSection>
                {
                    new ReportSection
                    {
                        Title = "Chi tiết sản phẩm",
                        Tables = new List<ReportTable>
                        {
                            new ReportTable
                            {
                                Headers = new List<string> { "Tên sản phẩm", "Giá", "Danh mục", "Ngày tạo" },
                                Rows = products.Select(p => new List<string>
                                {
                                    p.Name,
                                    p.Price.ToString("C"),
                                    p.Category?.Name ?? "N/A",
                                    p.CreatedOn.ToString("dd/MM/yyyy")
                                }).ToList()
                            }
                        }
                    }
                }
            };

            var pdfResult = await _pdfService.CreateReportPdfAsync(reportData, new PdfOptions { Title = "Products Report" }, cancellationToken);
            return new MemoryStream(pdfResult);
        }

        // Default: Excel
        var exportData = products.Select(p => new
        {
            p.Name,
            p.Price,
            Category = p.Category?.Name ?? "N/A",
            p.Description,
            CreatedOn = p.CreatedOn.ToString("dd/MM/yyyy")
        });

        byte[] result = await _excelWriter.WriteAsync(exportData, "Products", null, "Danh sách Sản phẩm", cancellationToken);
        
        return new MemoryStream(result);
    }
}
