namespace Boilerate.Application.Common.Models;

/// <summary>
/// Advanced search với keyword trong các fields cụ thể.
/// </summary>
public class Search
{
    /// <summary>
    /// Gets or sets the keyword để search.
    /// </summary>
    public string? Keyword { get; set; }

    /// <summary>
    /// Gets or sets the danh sách fields để search (nếu null thì search tất cả fields).
    /// Support nested: "Category.Name".
    /// </summary>
    public string[]? Fields { get; set; }
}
