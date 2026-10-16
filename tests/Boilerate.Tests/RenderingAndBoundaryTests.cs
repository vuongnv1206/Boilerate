using Boilerate.Application.Common.Exporters;
using Boilerate.Application.Common.Models;
using Boilerate.Application.Common.Specification;
using Boilerate.Domain.Identity;
using Boilerate.Infrastructure.Exporters;
using Boilerate.Infrastructure.FileStorage;
using Microsoft.AspNetCore.Http;
using PdfSharp.Pdf.IO;
using Xunit;

namespace Boilerate.Tests;

public sealed class RenderingAndBoundaryTests
{
    [Fact]
    public async Task PdfEmbedsUnicodeFontsAndPaginatesLongReports()
    {
        var service = new PdfSharpPdfService();
        var report = new ReportData
        {
            Title = "Báo cáo tiếng Việt",
            Sections = new()
            {
                new ReportSection { Title = "Danh sách", Tables = new()
                    { new ReportTable { Headers = new() { "Tên", "Giá trị" },
                        Rows = Enumerable.Range(1, 150).Select(i => new List<string> { "Nguyễn Thị Hương " + i, i.ToString() }).ToList() } } }
            }
        };
        var bytes = await service.CreateReportPdfAsync(report);
        using var pdf = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Import);
        Assert.Equal(report.Title, pdf.Info.Title); Assert.True(pdf.PageCount > 1);
        Assert.NotNull(pdf.Pages[0].Elements.GetDictionary("/Resources")?.Elements.GetDictionary("/Font"));
        var invoice = await service.CreateInvoicePdfAsync(new InvoiceData
            { InvoiceNumber = "HD-001", CompanyName = "Công ty Dịch vụ Việt", DueDate = DateTime.UtcNow.AddDays(30), CustomerName = "Nguyễn Văn An", Items = new() { new("Dịch vụ", 2, 125000) } });
        using var invoicePdf = PdfReader.Open(new MemoryStream(invoice), PdfDocumentOpenMode.Import);
        Assert.Equal(1, invoicePdf.PageCount);
        var output = Environment.GetEnvironmentVariable("BOILERATE_TEST_ARTIFACTS");
        if (!string.IsNullOrWhiteSpace(output))
        {
            Directory.CreateDirectory(output);
            await File.WriteAllBytesAsync(Path.Combine(output, "report-vietnamese.pdf"), bytes);
            await File.WriteAllBytesAsync(Path.Combine(output, "invoice-vietnamese.pdf"), invoice);
        }
    }
    [Fact]
    public void SearchAndPaginationRejectSecretsAndUnboundedRequests()
    {
        Assert.Throws<FluentValidation.ValidationException>(() => new EntitiesByPaginationFilterSpec<ApplicationUser>(new PaginationFilter { PageSize = 1000 }));
        Assert.Throws<FluentValidation.ValidationException>(() => new EntitiesByPaginationFilterSpec<ApplicationUser>(new PaginationFilter { OrderBy = new[] { "PasswordHash" } }));
    }
    [Fact]
    public async Task ImageUploadsCheckContentAndRemovalIsConfinedToStorage()
    {
        var service = new LocalFileStorageService();
        using var stream = new MemoryStream("not an image"u8.ToArray());
        var file = new FormFile(stream, 0, stream.Length, "image", "photo.png");
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => service.UploadAsync<ApplicationUser>(file, Boilerate.Domain.Common.FileType.Image));
        Assert.Throws<Boilerate.Application.Common.Exceptions.ForbiddenException>(() => service.Remove(Path.Combine(Path.GetTempPath(), "outside.jpg")));
    }
}
