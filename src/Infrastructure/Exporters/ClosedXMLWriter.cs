using System.Data;
using System.Globalization;
using System.Reflection;
using Boilerate.Application.Common.Exporters;
using ClosedXML.Excel;
using Microsoft.Extensions.Logging;

namespace Boilerate.Infrastructure.Exporters;

/// <summary>
/// Excel writer implementation using ClosedXML.
/// </summary>
public class ClosedXMLWriter : IExcelWriter
{
    private readonly ILogger<ClosedXMLWriter> _logger;

    public ClosedXMLWriter(ILogger<ClosedXMLWriter> logger)
    {
        _logger = logger;
    }

    public Task<byte[]> WriteAsync<T>(
        IEnumerable<T> data,
        string sheetName = "Sheet1",
        List<string>? headers = null,
        string? title = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        cancellationToken.ThrowIfCancellationRequested();
        PropertyInfo[] properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetMethod is { IsPublic: true } &&
                property.GetIndexParameters().Length == 0 && IsExportableType(property.PropertyType))
            .ToArray();
        if (properties.Length == 0)
        {
            throw new ArgumentException($"Type '{typeof(T).Name}' has no public readable scalar properties to export.", nameof(data));
        }

        return Task.Run(() =>
        {
            using var workbook = new XLWorkbook();
            IXLWorksheet worksheet = workbook.Worksheets.Add(sheetName);
            int headerRow = WriteTitle(worksheet, title, properties.Length);
            for (int index = 0; index < properties.Length; index++)
            {
                string header = headers is not null && index < headers.Count
                    ? headers[index]
                    : FormatPropertyName(properties[index].Name);
                SetText(worksheet.Cell(headerRow, index + 1), header);
            }

            StyleHeader(worksheet.Range(headerRow, 1, headerRow, properties.Length));

            int currentRow = headerRow + 1;
            foreach (T item in data)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (int index = 0; index < properties.Length; index++)
                {
                    IXLCell cell = worksheet.Cell(currentRow, index + 1);
                    SetCellValue(cell, properties[index].GetValue(item));
                    cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    cell.Style.Border.OutsideBorderColor = XLColor.LightGray;
                    if ((currentRow - headerRow) % 2 != 0)
                    {
                        cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#DDEBF7");
                    }
                }

                currentRow++;
            }

            int rowCount = currentRow - headerRow - 1;
            if (rowCount > 0)
            {
                worksheet.Range(headerRow, 1, currentRow - 1, properties.Length).SetAutoFilter();
            }

            FinishWorksheet(worksheet, headerRow, properties.Length, cancellationToken);
            _logger.LogInformation("Exported {Count} rows to Excel sheet '{SheetName}'", rowCount, sheetName);
            return SaveWorkbookToBytes(workbook, cancellationToken);
        }, cancellationToken);
    }

    public Task<byte[]> WriteAsync(
        DataTable dataTable,
        string sheetName = "Sheet1",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataTable);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() =>
        {
            using var workbook = new XLWorkbook();
            WriteDataTable(workbook, dataTable, sheetName, cancellationToken);
            _logger.LogInformation("Exported DataTable with {Count} rows to Excel sheet '{SheetName}'", dataTable.Rows.Count, sheetName);
            return SaveWorkbookToBytes(workbook, cancellationToken);
        }, cancellationToken);
    }

    public Task<byte[]> WriteAsync(Dictionary<string, DataTable> sheets, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sheets);
        if (sheets.Count == 0)
        {
            throw new ArgumentException("At least one worksheet is required.", nameof(sheets));
        }

        cancellationToken.ThrowIfCancellationRequested();
        return Task.Run(() =>
        {
            using var workbook = new XLWorkbook();
            foreach (KeyValuePair<string, DataTable> sheet in sheets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                WriteDataTable(workbook, sheet.Value, sheet.Key, cancellationToken);
            }

            _logger.LogInformation("Exported {Count} sheets to Excel workbook", sheets.Count);
            return SaveWorkbookToBytes(workbook, cancellationToken);
        }, cancellationToken);
    }

    private static int WriteTitle(IXLWorksheet worksheet, string? title, int columnCount)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return 1;
        }

        IXLRange titleRange = worksheet.Range(1, 1, 1, columnCount).Merge();
        SetText(titleRange.FirstCell(), title);
        titleRange.Style.Font.Bold = true;
        titleRange.Style.Font.FontSize = 16;
        titleRange.Style.Font.FontColor = XLColor.FromHtml("#1F4E78");
        titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        return 2;
    }

    private static void WriteDataTable(XLWorkbook workbook, DataTable dataTable, string sheetName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dataTable);
        if (dataTable.Columns.Count == 0)
        {
            throw new ArgumentException("A worksheet requires at least one column.", nameof(dataTable));
        }

        cancellationToken.ThrowIfCancellationRequested();
        IXLWorksheet worksheet = workbook.Worksheets.Add(sheetName);
        for (int column = 0; column < dataTable.Columns.Count; column++)
        {
            SetText(worksheet.Cell(1, column + 1), dataTable.Columns[column].ColumnName);
        }

        int currentRow = 2;
        foreach (DataRow row in dataTable.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (int column = 0; column < dataTable.Columns.Count; column++)
            {
                SetCellValue(worksheet.Cell(currentRow, column + 1), row[column], booleanAsText: false);
            }

            currentRow++;
        }

        if (dataTable.Rows.Count > 0)
        {
            worksheet.Range(1, 1, currentRow - 1, dataTable.Columns.Count).CreateTable();
        }

        StyleHeader(worksheet.Range(1, 1, 1, dataTable.Columns.Count));
        FinishWorksheet(worksheet, 1, dataTable.Columns.Count, cancellationToken);
    }

    private static void StyleHeader(IXLRange header)
    {
        header.Style.Font.Bold = true;
        header.Style.Font.FontColor = XLColor.White;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#4F81BD");
        header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        header.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        header.Style.Border.OutsideBorderColor = XLColor.White;
    }

    private static void FinishWorksheet(IXLWorksheet worksheet, int headerRow, int columnCount, CancellationToken cancellationToken)
    {
        for (int column = 1; column <= columnCount; column++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            worksheet.Column(column).AdjustToContents();
        }

        worksheet.SheetView.FreezeRows(headerRow);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void SetCellValue(IXLCell cell, object? value, bool booleanAsText = true)
    {
        if (value is null or DBNull)
        {
            cell.Value = string.Empty;
        }
        else if (value is DateTime dateTime)
        {
            cell.Value = dateTime;
            cell.Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
        }
        else if (value is DateTimeOffset dateTimeOffset)
        {
            cell.Value = dateTimeOffset.DateTime;
            cell.Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
        }
        else if (value is bool boolean)
        {
            if (booleanAsText)
            {
                cell.Value = boolean ? "Yes" : "No";
            }
            else
            {
                cell.Value = boolean;
            }
        }
        else if (value is decimal amount)
        {
            string exactValue = amount.ToString("0.############################", CultureInfo.InvariantCulture);
            if (exactValue.Replace(".", string.Empty, StringComparison.Ordinal).TrimStart('-', '0').Length > 15)
            {
                SetExactText(cell, exactValue);
            }
            else
            {
                cell.Value = amount;
                int decimalPoint = exactValue.IndexOf('.');
                int fractionalDigits = decimalPoint < 0 ? 0 : exactValue.Length - decimalPoint - 1;
                // More display places would expose the binary double representation used by Excel.
                cell.Style.NumberFormat.Format = "#,##0.00" + new string('#', Math.Max(0, fractionalDigits - 2));
            }
        }
        else if (value is sbyte or byte or short or ushort or int or uint or long or ulong)
        {
            string exactValue = Convert.ToString(value, CultureInfo.InvariantCulture)!;
            if (exactValue.TrimStart('-').Length > 15)
            {
                // Excel retains only 15 significant numeric digits; identifiers must remain exact.
                SetExactText(cell, exactValue);
            }
            else
            {
                cell.Value = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                cell.Style.NumberFormat.Format = "#,##0";
            }
        }
        else if (value is double or float)
        {
            double number = value is float single && float.IsFinite(single)
                ? double.Parse(single.ToString("R", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture)
                : Convert.ToDouble(value, CultureInfo.InvariantCulture);
            if (double.IsFinite(number))
            {
                cell.Value = number;
                cell.Style.NumberFormat.Format = "General";
            }
            else
            {
                SetExactText(cell, number.ToString(CultureInfo.InvariantCulture));
            }
        }
        else
        {
            SetText(cell, value.ToString());
        }
    }

    private static void SetExactText(IXLCell cell, string value)
    {
        SetText(cell, value);
        cell.Style.NumberFormat.Format = "@";
    }

    private static void SetText(IXLCell cell, string? value)
    {
        // ClosedXML consumes one leading apostrophe as its quote prefix; retain a literal one in exported text.
        cell.Value = value?.StartsWith('\'') == true ? "'" + value : value;
    }

    private static bool IsExportableType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(DateTime) ||
            type == typeof(DateTimeOffset) || type == typeof(decimal) || type == typeof(Guid);
    }

    private static string FormatPropertyName(string propertyName) =>
        System.Text.RegularExpressions.Regex.Replace(propertyName, "([a-z])([A-Z])", "$1 $2");

    private static byte[] SaveWorkbookToBytes(XLWorkbook workbook, CancellationToken cancellationToken)
    {
        // ClosedXML serialization is synchronous; do not return a result if cancellation occurs while saving.
        cancellationToken.ThrowIfCancellationRequested();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        cancellationToken.ThrowIfCancellationRequested();
        return stream.ToArray();
    }
}
