using Boilerate.Application.Common.Interfaces;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace Boilerate.Application.Common.Exporters;

/// <summary>
/// Service for exporting data to Excel format
/// </summary>
public interface IExcelWriter : ITransientService
{
    /// <summary>
    /// Write data to Excel file
    /// </summary>
    /// <typeparam name="T">Type of data to export</typeparam>
    /// <param name="data">Collection of data</param>
    /// <param name="sheetName">Sheet name in Excel</param>
    /// <param name="headers">Optional custom headers (nếu null, dùng property names)</param>
    /// <param name="title">Optional document title.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<byte[]> WriteAsync<T>(
        IEnumerable<T> data,
        string sheetName = "Sheet1",
        List<string>? headers = null,
        string? title = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Write DataTable to Excel file
    /// </summary>
    Task<byte[]> WriteAsync(
        DataTable dataTable,
        string sheetName = "Sheet1",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Write multiple sheets to Excel file
    /// </summary>
    Task<byte[]> WriteAsync(
        Dictionary<string, DataTable> sheets,
        CancellationToken cancellationToken = default);
}
