using Boilerate.Infrastructure.Exporters;
using Boilerate.Infrastructure.Mailing;
using ClosedXML.Excel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Boilerate.Tests;

public sealed class ExportAndTemplateTests
{
    [Theory]
    [InlineData("email-confirmation")]
    [InlineData("password-reset")]
    public void RazorTemplatesCompileWithResolvedCompilerDependencies(string template)
    {
        var service = new EmailTemplateService();
        var html = service.GenerateEmailTemplate(template, new TemplateModel());

        Assert.Contains("https://example.test/confirm", html);
    }

    [Fact]
    public async Task ExcelExportRetainsNumbersAndUnicode()
    {
        var writer = new ClosedXMLWriter(NullLogger<ClosedXMLWriter>.Instance);
        var bytes = await writer.WriteAsync(new[] { new ExportRow { Name = "Nguy\u1ec5n", Amount = 12.5m } });
        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet(1);

        Assert.Equal("Nguy\u1ec5n", sheet.Cell(2, 1).GetString());
        Assert.Equal(12.5m, sheet.Cell(2, 2).GetValue<decimal>());
    }

    public sealed class TemplateModel
    {
        public string UserName { get; set; } = "test-user";
        public string Email { get; set; } = "test@example.test";
        public string Url { get; set; } = "https://example.test/confirm";
    }

    public sealed class ExportRow
    {
        public string Name { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }
}