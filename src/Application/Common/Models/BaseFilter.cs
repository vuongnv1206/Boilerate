namespace Boilerate.Application.Common.Models;

/// <summary>
/// Base filter cho mọi search requests.
/// </summary>
public class BaseFilter
{
    /// <summary>
    /// Gets or sets generic keyword search (search trong tất cả fields).
    /// </summary>
    public string? Keyword { get; set; }

    /// <summary>
    /// Gets or sets the advanced search với fields cụ thể.
    /// </summary>
    public Search? AdvancedSearch { get; set; }

    /// <summary>
    /// Gets or sets the advanced filter với operators và logic.
    /// </summary>
    public Filter? AdvancedFilter { get; set; }
}
