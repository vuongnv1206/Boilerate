using System.Data;
using System.Globalization;
using Boilerate.Infrastructure.Exporters;
using ClosedXML.Excel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Boilerate.Tests;

public sealed class ExportMaintenanceTests
{
    [Fact]
    public async Task GenericExportSkipsIndexersAndPrivateGettersAndIncludesEnums()
    {
        byte[] bytes = await CreateWriter().WriteAsync(new[] { new ReadableRow() });
        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        IXLWorksheet sheet = workbook.Worksheet(1);

        Assert.Equal(3, sheet.LastColumnUsed()!.ColumnNumber());
        Assert.Equal("Name", sheet.Cell(1, 1).GetString());
        Assert.Equal("Active", sheet.Cell(2, 2).GetString());
        Assert.True(sheet.Cell(2, 3).IsEmpty());
    }

    [Fact]
    public async Task FractionalNumbersRemainVisible()
    {
        byte[] bytes = await CreateWriter().WriteAsync(new[] { new MeasurementRow { Ratio = 1.75, Delta = 0.125f, Amount = 1.2345m } });
        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        IXLWorksheet sheet = workbook.Worksheet(1);

        Assert.Equal("1.75", sheet.Cell(2, 1).GetFormattedString(CultureInfo.InvariantCulture));
        Assert.Equal("0.125", sheet.Cell(2, 2).GetFormattedString(CultureInfo.InvariantCulture));
        Assert.Equal("1.2345", sheet.Cell(2, 3).GetFormattedString(CultureInfo.InvariantCulture));
        Assert.Equal(XLDataType.Number, sheet.Cell(2, 1).DataType);
    }

    [Theory]
    [InlineData("12", "12.00")]
    [InlineData("1.2300", "1.23")]
    [InlineData("0.0000000000000000000000000001", "0.0000000000000000000000000001")]
    public async Task DecimalDisplayUsesItsActualFractionalScale(string value, string expectedDisplay)
    {
        decimal amount = decimal.Parse(value, CultureInfo.InvariantCulture);
        byte[] bytes = await CreateWriter().WriteAsync(new[] { new { Amount = amount } });
        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        IXLCell cell = workbook.Worksheet(1).Cell(2, 1);

        Assert.Equal(XLDataType.Number, cell.DataType);
        Assert.Equal(expectedDisplay, cell.GetFormattedString(CultureInfo.InvariantCulture));
        Assert.Equal(amount, cell.GetValue<decimal>());
    }

    [Fact]
    public async Task FloatDisplayDoesNotExposeDoubleWideningArtifacts()
    {
        byte[] bytes = await CreateWriter().WriteAsync(new[] { new { Measurement = 0.1f } });
        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        IXLCell cell = workbook.Worksheet(1).Cell(2, 1);

        Assert.Equal(XLDataType.Number, cell.DataType);
        Assert.Equal("0.1", cell.GetFormattedString(CultureInfo.InvariantCulture));
        Assert.Equal(0.1f, cell.GetValue<float>());
    }

    [Fact]
    public async Task TextValuesHeadersAndTitlesRoundTripWithoutExecutingFormulas()
    {
        string[] values = ["'Literal", "''Literal", "=HYPERLINK(\"https://example.test\",\"click\")", "+1+1", "@SUM(1,2)"];
        byte[] bytes = await CreateWriter().WriteAsync(values.Select(value => new { Text = value }),
            headers: ["'Header"], title: "'Title");
        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        IXLWorksheet sheet = workbook.Worksheet(1);

        Assert.Equal("'Title", sheet.Cell(1, 1).GetString());
        Assert.Equal("'Header", sheet.Cell(2, 1).GetString());
        for (int index = 0; index < values.Length; index++)
        {
            IXLCell cell = sheet.Cell(index + 3, 1);
            Assert.Equal(values[index], cell.GetString());
            Assert.Equal(XLDataType.Text, cell.DataType);
            Assert.False(cell.HasFormula);
        }
    }

    [Fact]
    public async Task DataTableTextPreservesLiteralApostrophesAndFormulaLikeValues()
    {
        using var table = new DataTable();
        table.Columns.Add("'Header", typeof(string));
        table.Rows.Add("'Literal");
        table.Rows.Add("=1+1");
        byte[] bytes = await CreateWriter().WriteAsync(table);
        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        IXLWorksheet sheet = workbook.Worksheet(1);

        Assert.Equal("'Header", sheet.Cell(1, 1).GetString());
        Assert.Equal("'Literal", sheet.Cell(2, 1).GetString());
        Assert.Equal("=1+1", sheet.Cell(3, 1).GetString());
        Assert.Equal(XLDataType.Text, sheet.Cell(3, 1).DataType);
        Assert.False(sheet.Cell(3, 1).HasFormula);
    }

    [Theory]
    [InlineData(long.MaxValue)]
    [InlineData(long.MinValue)]
    [InlineData(1000000000000000L)]
    public async Task LongIdentifiersKeepEveryDigit(long identifier)
    {
        byte[] bytes = await CreateWriter().WriteAsync(new[] { new { Identifier = identifier } });
        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        IXLCell cell = workbook.Worksheet(1).Cell(2, 1);

        Assert.Equal(XLDataType.Text, cell.DataType);
        Assert.Equal(identifier.ToString(CultureInfo.InvariantCulture), cell.GetString());
    }

    [Fact]
    public async Task DataTableUsesTheSamePrecisionProtectionAndRetainsBooleanCells()
    {
        using var table = new DataTable();
        table.Columns.Add("ExternalId", typeof(ulong));
        table.Columns.Add("Amount", typeof(decimal));
        table.Columns.Add("Active", typeof(bool));
        table.Columns.Add("Count", typeof(int));
        table.Rows.Add(ulong.MaxValue, 123456789012345.67m, true, 12);

        byte[] bytes = await CreateWriter().WriteAsync(table);
        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        IXLWorksheet sheet = workbook.Worksheet(1);

        Assert.Equal(ulong.MaxValue.ToString(CultureInfo.InvariantCulture), sheet.Cell(2, 1).GetString());
        Assert.Equal("123456789012345.67", sheet.Cell(2, 2).GetString());
        Assert.Equal(XLDataType.Text, sheet.Cell(2, 2).DataType);
        Assert.Equal(XLDataType.Boolean, sheet.Cell(2, 3).DataType);
        Assert.True(sheet.Cell(2, 3).GetBoolean());
        Assert.Equal(XLDataType.Number, sheet.Cell(2, 4).DataType);
        Assert.Single(sheet.Tables);
    }

    [Fact]
    public async Task EmptyTypedExportRetainsItsTitleAndHeaders()
    {
        byte[] bytes = await CreateWriter().WriteAsync(Array.Empty<ReadableRow>(), title: "Inventory");
        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        IXLWorksheet sheet = workbook.Worksheet(1);

        Assert.Equal("Inventory", sheet.Cell(1, 1).GetString());
        Assert.Equal("Name", sheet.Cell(2, 1).GetString());
        Assert.Equal("Status", sheet.Cell(2, 2).GetString());
        Assert.True(sheet.Cell(3, 1).IsEmpty());
        Assert.Equal(3, sheet.MergedRanges.Single().ColumnCount());
    }

    [Fact]
    public async Task MissingColumnsAndEmptyWorkbookHaveClearErrors()
    {
        ClosedXMLWriter writer = CreateWriter();
        ArgumentException noProperties = await Assert.ThrowsAsync<ArgumentException>(() =>
            writer.WriteAsync(new[] { new NoExportableColumns() }, title: "Inventory"));
        using var table = new DataTable();
        ArgumentException noTableColumns = await Assert.ThrowsAsync<ArgumentException>(() => writer.WriteAsync(table));
        ArgumentException noSheets = await Assert.ThrowsAsync<ArgumentException>(() => writer.WriteAsync(new Dictionary<string, DataTable>()));

        Assert.Equal("data", noProperties.ParamName);
        Assert.Equal("dataTable", noTableColumns.ParamName);
        Assert.Equal("sheets", noSheets.ParamName);
    }

    [Fact]
    public async Task CancellationStopsLazyEnumerationBeforeItConsumesTheRestOfTheSource()
    {
        using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateWriter().WriteAsync(Rows(), cancellationToken: cancellation.Token));

        IEnumerable<ReadableRow> Rows()
        {
            yield return new ReadableRow();
            cancellation.Cancel();
            yield return new ReadableRow();
            throw new InvalidOperationException("The canceled export must not enumerate the remaining source.");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DataTableAndMultipleSheetExportsObserveCancellationDuringRows(bool multipleSheets)
    {
        using var cancellation = new CancellationTokenSource();
        using var table = new DataTable();
        table.Columns.Add("Value", typeof(object));
        table.Rows.Add(new CancelingCell(cancellation));
        table.Rows.Add("This row must not be exported.");
        ClosedXMLWriter writer = CreateWriter();

        if (multipleSheets)
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => writer.WriteAsync(
                new Dictionary<string, DataTable> { ["First"] = table, ["Second"] = table }, cancellation.Token));
        }
        else
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => writer.WriteAsync(table, cancellationToken: cancellation.Token));
        }
    }

    private static ClosedXMLWriter CreateWriter() => new(NullLogger<ClosedXMLWriter>.Instance);

    private enum ExportStatus
    {
        Active = 1
    }

    private sealed class ReadableRow
    {
        public string Name { get; set; } = "Product";
        public ExportStatus Status { get; set; } = ExportStatus.Active;
        public ExportStatus? OptionalStatus { get; set; }
        public string PrivateValue { private get; set; } = "Hidden";
        public string this[int index] => throw new InvalidOperationException("An indexer is not an export column.");
    }

    private sealed class MeasurementRow
    {
        public double Ratio { get; set; }
        public float Delta { get; set; }
        public decimal Amount { get; set; }
    }

    private sealed class NoExportableColumns
    {
        public object Value { get; set; } = new();
    }

    private sealed class CancelingCell(CancellationTokenSource cancellation)
    {
        public override string ToString()
        {
            cancellation.Cancel();
            return "Cancellation requested during conversion.";
        }
    }
}
