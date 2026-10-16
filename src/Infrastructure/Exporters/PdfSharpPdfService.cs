using Boilerate.Application.Common.Exporters;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;
using System.Globalization;

namespace Boilerate.Infrastructure.Exporters;

/// <summary>Invoice/report rendering using the MIT-licensed 6.2.4 Core build and embedded OFL fonts.</summary>
public sealed class PdfSharpPdfService : IPdfService
{
    static PdfSharpPdfService()
    {
        if (GlobalFontSettings.FontResolver is null) GlobalFontSettings.FontResolver = new NotoFontResolver();
    }

    private static Document Document(PdfOptions options)
    {
        var document = new Document();
        document.Info.Title = options.Title; document.Info.Author = options.Author; document.Info.Subject = options.Subject;
        document.Styles["Normal"]!.Font.Name = "Noto Sans";
        document.Styles["Normal"]!.Font.Size = 10;
        var section = document.AddSection();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.TopMargin = Unit.FromCentimeter(2.5);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(2.5);
        section.PageSetup.LeftMargin = Unit.FromCentimeter(2);
        section.PageSetup.RightMargin = Unit.FromCentimeter(2);
        if (options.IncludeHeader) HeaderFooter(section.Headers.Primary, options.HeaderLeft, options.HeaderRight);
        if (options.IncludeFooter) HeaderFooter(section.Footers.Primary, options.FooterLeft, options.FooterRight);
        if (options.ShowPageNumbers)
        {
            var p = section.Footers.Primary.AddParagraph(); p.Format.Alignment = ParagraphAlignment.Right;
            p.AddPageField(); p.AddText(" / "); p.AddNumPagesField();
        }
        return document;
    }

    private static void HeaderFooter(HeaderFooter area, string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) && string.IsNullOrWhiteSpace(right)) return;
        var paragraph = area.AddParagraph(left + "\t" + right);
        paragraph.Format.TabStops.AddTabStop(Unit.FromCentimeter(17), TabAlignment.Right);
    }
    private static void OptionalText(Section section, params string[] values)
    {
        var text = string.Join(" | ", values.Where(x => !string.IsNullOrWhiteSpace(x)));
        if (text.Length != 0) section.AddParagraph(text);
    }

    private static string Money(decimal value) => value.ToString("N2", CultureInfo.GetCultureInfo("vi-VN"));
    private static void Heading(Section section, string text)
    {
        var p = section.AddParagraph(text); p.Format.Font.Bold = true; p.Format.Font.Size = 16;
        p.Format.SpaceBefore = Unit.FromPoint(12); p.Format.SpaceAfter = Unit.FromPoint(8);
    }
    private static Table Table(Section section, IReadOnlyList<string> headers)
    {
        var table = section.AddTable();
        table.Borders.Width = Unit.FromPoint(0.5);
        foreach (var _ in headers) table.AddColumn(Unit.FromCentimeter(17d / headers.Count));
        var row = table.AddRow(); row.HeadingFormat = true; row.Format.Font.Bold = true;
        for (var i = 0; i < headers.Count; i++) row.Cells[i].AddParagraph(headers[i]);
        return table;
    }
    private static byte[] Render(Document document, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();
        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, false);
        ct.ThrowIfCancellationRequested();
        return stream.ToArray();
    }

    public Task<byte[]> CreateInvoicePdfAsync(InvoiceData data, PdfOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (data.Items.Count > 10000 || data.Items.Any(i => i.Quantity < 0 || i.UnitPrice < 0) || data.TaxRate < 0 || data.TaxRate > 1)
            throw new FluentValidation.ValidationException("Invalid invoice quantities, prices, tax rate or row count.");
        var document = Document(options ?? new PdfOptions { Title = "Hóa đơn " + data.InvoiceNumber });
        var section = document.LastSection;
        Heading(section, "HÓA ĐƠN " + data.InvoiceNumber);
        section.AddParagraph(data.CompanyName);
        OptionalText(section, data.CompanyAddress);
        OptionalText(section, data.CompanyPhone, data.CompanyEmail);
        section.AddParagraph("Ngày: " + data.InvoiceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        if (data.DueDate != default) section.AddParagraph("Hạn thanh toán: " + data.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Heading(section, "Khách hàng");
        section.AddParagraph(data.CustomerName);
        OptionalText(section, data.CustomerAddress);
        OptionalText(section, data.CustomerPhone, data.CustomerEmail);
        var table = Table(section, new[] { "Nội dung", "Số lượng", "Đơn giá", "Thành tiền" });
        foreach (var item in data.Items)
        {
            var row = table.AddRow();
            var cells = new[] { item.Description, item.Quantity.ToString(CultureInfo.InvariantCulture), Money(item.UnitPrice), Money(item.Total) };
            for (var i = 0; i < cells.Length; i++) row.Cells[i].AddParagraph(cells[i]);
        }
        section.AddParagraph("Tạm tính: " + Money(data.Subtotal));
        section.AddParagraph("Thuế (" + (data.TaxRate * 100).ToString(CultureInfo.InvariantCulture) + "%): " + Money(data.TaxAmount));
        Heading(section, "Tổng cộng: " + Money(data.Total));
        OptionalText(section, data.Notes); OptionalText(section, data.PaymentTerms);
        return Task.FromResult(Render(document, cancellationToken));
    }

    public Task<byte[]> CreateReportPdfAsync(ReportData data, PdfOptions? options = null, CancellationToken cancellationToken = default)
    {
        var document = Document(options ?? new PdfOptions { Title = data.Title });
        var section = document.LastSection;
        Heading(section, data.Title); section.AddParagraph(data.Subtitle);
        section.AddParagraph(data.GeneratedDate.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC | " + data.GeneratedBy);
        section.AddParagraph(data.Summary);
        foreach (var metadata in data.Metadata) section.AddParagraph(metadata.Key + ": " + metadata.Value);
        foreach (var part in data.Sections)
        {
            Heading(section, part.Title); section.AddParagraph(part.Content);
            foreach (var reportTable in part.Tables)
            {
                if (reportTable.Headers.Count == 0 || reportTable.Rows.Count > 10000 ||
                    reportTable.Rows.Any(r => r.Count != reportTable.Headers.Count))
                    throw new FluentValidation.ValidationException("Report tables need headers and matching cells (maximum 10000 rows).");
                section.AddParagraph(reportTable.Title);
                var table = Table(section, reportTable.Headers);
                foreach (var cells in reportTable.Rows)
                {
                    var row = table.AddRow();
                    for (var i = 0; i < cells.Count; i++) row.Cells[i].AddParagraph(cells[i]);
                }
            }
            if (part.ChartImage is { Length: > 0 })
                section.AddImage("base64:" + Convert.ToBase64String(part.ChartImage)).Width = Unit.FromCentimeter(16);
        }
        return Task.FromResult(Render(document, cancellationToken));
    }

    private sealed class NotoFontResolver : IFontResolver
    {
        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
            new(isBold ? "NotoSans-Bold" : "NotoSans-Regular", false, isItalic);
        public byte[] GetFont(string faceName)
        {
            using var stream = typeof(PdfSharpPdfService).Assembly.GetManifestResourceStream(
                "Boilerate.Infrastructure.Exporters.Fonts." + faceName + ".ttf")
                ?? throw new InvalidOperationException("Embedded PDF font is missing.");
            using var memory = new MemoryStream(); stream.CopyTo(memory); return memory.ToArray();
        }
    }
}
