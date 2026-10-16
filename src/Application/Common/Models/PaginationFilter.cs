namespace Boilerate.Application.Common.Models;

/// <summary>
/// Pagination filter kế thừa BaseFilter, thêm pagination và sorting.
/// </summary>
public class PaginationFilter : BaseFilter
{
    /// <summary>
    /// Gets or sets the page number (bắt đầu từ 1).
    /// </summary>
    public int PageNumber { get; set; } = 1;

    /// <summary>
    /// Gets or sets the page size (số items mỗi page).
    /// </summary>
    public int PageSize { get; set; } = 10;

    /// <summary>
    /// Gets or sets the OrderBy fields: ["Name", "Price Desc", "Category.Name"].
    /// </summary>
    public string[]? OrderBy { get; set; }
}

public static class PaginationFilterExtensions
{
    public static bool HasOrderBy(this PaginationFilter filter) =>
        filter.OrderBy?.Length > 0;
}
