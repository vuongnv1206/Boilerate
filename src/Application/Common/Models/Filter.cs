namespace Boilerate.Application.Common.Models;

/// <summary>
/// Advanced filter với operators và logic.
/// </summary>
public class Filter
{
    /// <summary>
    /// Gets or sets the logic operator: "and", "or", "xor" (dùng khi có nhiều filters).
    /// </summary>
    public string? Logic { get; set; }

    /// <summary>
    /// Gets or sets the field name (support nested: "Category.Name").
    /// </summary>
    public string? Field { get; set; }

    /// <summary>
    /// Gets or sets the operator: "eq", "neq", "gt", "gte", "lt", "lte", "contains", "startswith", "endswith".
    /// </summary>
    public string? Operator { get; set; }

    /// <summary>
    /// Gets or sets the value để compare.
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// Gets or sets the nested filters (dùng khi có Logic).
    /// </summary>
    public List<Filter>? Filters { get; set; }
}

/// <summary>
/// Filter operators constants.
/// </summary>
public static class FilterOperator
{
    /// <summary>Equal: ==.</summary>
    public const string EQ = "eq";

    /// <summary>Not Equal: !=.</summary>
    public const string NEQ = "neq";

    /// <summary>Less Than: &lt;.</summary>
    public const string LT = "lt";

    /// <summary>Less Than or Equal: &lt;=.</summary>
    public const string LTE = "lte";

    /// <summary>Greater Than: &gt;.</summary>
    public const string GT = "gt";

    /// <summary>Greater Than or Equal: &gt;=.</summary>
    public const string GTE = "gte";

    /// <summary>String Contains.</summary>
    public const string CONTAINS = "contains";

    /// <summary>String Starts With.</summary>
    public const string STARTSWITH = "startswith";

    /// <summary>String Ends With.</summary>
    public const string ENDSWITH = "endswith";
}

/// <summary>
/// Filter logic operators constants.
/// </summary>
public static class FilterLogic
{
    /// <summary>AND logic: &amp;&amp;.</summary>
    public const string AND = "and";

    /// <summary>OR logic: ||.</summary>
    public const string OR = "or";

    /// <summary>XOR logic: ^.</summary>
    public const string XOR = "xor";
}
