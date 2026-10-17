using System.Globalization;
using System.Text.Json.Serialization;
using Ardalis.Specification;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Models;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;

namespace Boilerate.Application.Common.Specification;

public static class SpecificationBuilderExtensions
{
    #region SearchBy & PaginateBy (Entry Points)

    /// <summary>
    /// Extension method để apply tất cả search và filter từ BaseFilter vào specification.
    /// </summary>
    public static ISpecificationBuilder<T> SearchBy<T>(this ISpecificationBuilder<T> query, BaseFilter filter) =>
        query
            .SearchByKeyword(filter.Keyword)
            .AdvancedSearch(filter.AdvancedSearch)
            .AdvancedFilter(filter.AdvancedFilter);

    /// <summary>
    /// Extension method để apply pagination và ordering vào specification.
    /// </summary>
    public static ISpecificationBuilder<T> PaginateBy<T>(this ISpecificationBuilder<T> query, PaginationFilter filter)
    {
        if (filter.PageNumber < 1 || filter.PageSize < 1 || filter.PageSize > 100 || ((long)filter.PageNumber - 1) * filter.PageSize > int.MaxValue)
            throw new FluentValidation.ValidationException("Page size must be 1-100 and page number must be positive.");

        // Calculate skip
        if (filter.PageNumber > 1)
        {
            query = query.Skip((filter.PageNumber - 1) * filter.PageSize);
        }

        return query
            .Take(filter.PageSize)
            .OrderBy(filter.OrderBy);
    }

    #endregion

    #region Search Methods

    /// <summary>
    /// Extension method để search đơn giản với keyword trong tất cả fields.
    /// </summary>
    public static IOrderedSpecificationBuilder<T> SearchByKeyword<T>(
        this ISpecificationBuilder<T> specificationBuilder,
        string? keyword) =>
        specificationBuilder.AdvancedSearch(new Search { Keyword = keyword });

    /// <summary>
    /// Extension method để search nâng cao với keyword trong các fields cụ thể hoặc tất cả fields.
    /// </summary>
    public static IOrderedSpecificationBuilder<T> AdvancedSearch<T>(
        this ISpecificationBuilder<T> specificationBuilder,
        Search? search)
    {
        if (search?.Keyword?.Length > 200 || search?.Fields?.Length > 16)
        {
            throw new FluentValidation.ValidationException("Search permits at most 200 characters and 16 fields.");
        }

        if (!string.IsNullOrEmpty(search?.Keyword))
        {
            if (search.Fields?.Length > 0)
            {
                // Search trong các fields được chỉ định
                foreach (string field in search.Fields)
                {
                    var paramExpr = Expression.Parameter(typeof(T));
                    MemberExpression propertyExpr = GetPropertyExpression(field, paramExpr);
                    specificationBuilder.AddSearchPropertyByKeyword(propertyExpr, paramExpr, search.Keyword);
                }
            }
            else
            {
                // Search trong TẤT CẢ primitive fields
                foreach (var property in typeof(T).GetProperties()
                    .Where(prop => prop.PropertyType == typeof(string) && AllowedFields(typeof(T)).Contains(prop.Name) &&
                        (Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType) is { } propertyType
                        && !propertyType.IsEnum
                        && Type.GetTypeCode(propertyType) != TypeCode.Object))
                {
                    var paramExpr = Expression.Parameter(typeof(T));
                    var propertyExpr = Expression.Property(paramExpr, property);
                    specificationBuilder.AddSearchPropertyByKeyword(propertyExpr, paramExpr, search.Keyword);
                }
            }
        }

        return new OrderedSpecificationBuilder<T>(specificationBuilder.Specification);
    }

    /// <summary>
    /// Private helper method để thêm search criteria cho một property cụ thể.
    /// </summary>
    private static void AddSearchPropertyByKeyword<T>(
        this ISpecificationBuilder<T> specificationBuilder,
        Expression propertyExpr,
        ParameterExpression paramExpr,
        string keyword,
        string operatorSearch = FilterOperator.CONTAINS)
    {
        if (propertyExpr is not MemberExpression memberExpr || memberExpr.Member is not PropertyInfo property)
        {
            throw new ArgumentException("propertyExpr must be a property expression.", nameof(propertyExpr));
        }

        // Tạo search pattern
        string searchTerm = operatorSearch switch
        {
            FilterOperator.STARTSWITH => $"{keyword.ToLower()}%",
            FilterOperator.ENDSWITH => $"%{keyword.ToLower()}",
            FilterOperator.CONTAINS => $"%{keyword.ToLower()}%",
            _ => throw new ArgumentException("operatorSearch is not valid.", nameof(operatorSearch))
        };

        // Build selector expression
        Expression selectorExpr =
            property.PropertyType == typeof(string)
                ? propertyExpr
                : Expression.Condition(
                    Expression.Equal(
                    Expression.Convert(propertyExpr, typeof(object)),
                    Expression.Constant(null, typeof(object))),
                    Expression.Constant(null, typeof(string)),
                    Expression.Call(propertyExpr, "ToString", null, null));

        // Convert to lowercase
        var toLowerMethod = typeof(string).GetMethod("ToLower", Type.EmptyTypes);
        Expression callToLowerMethod = Expression.Call(selectorExpr, toLowerMethod!);
        var selector = Expression.Lambda<Func<T, string>>(callToLowerMethod, paramExpr);

        // Add to SearchCriterias
        ((List<SearchExpressionInfo<T>>)specificationBuilder.Specification.SearchCriterias)
            .Add(new SearchExpressionInfo<T>(selector, searchTerm, 1));
    }

    #endregion

    #region Filter Methods

    /// <summary>
    /// Extension method để apply advanced filter với operators và logic.
    /// </summary>
    public static IOrderedSpecificationBuilder<T> AdvancedFilter<T>(
        this ISpecificationBuilder<T> specificationBuilder,
        Filter? filter)
    {
        if (filter is not null)
        {
            ValidateFilterTree(filter);
            var parameter = Expression.Parameter(typeof(T));
            Expression binaryExpresioFilter;

            if (!string.IsNullOrEmpty(filter.Logic))
            {
                if (filter.Filters is null)
                {
                    throw new FluentValidation.ValidationException("The Filters attribute is required when declaring a logic");
                }

                binaryExpresioFilter = CreateFilterExpression(filter.Logic, filter.Filters, parameter);
            }
            else
            {
                var filterValid = GetValidFilter(filter);
                binaryExpresioFilter = CreateFilterExpression(
                    filterValid.Field!,
                    filterValid.Operator!,
                    filterValid.Value,
                    parameter);
            }

            ((List<WhereExpressionInfo<T>>)specificationBuilder.Specification.WhereExpressions)
                .Add(new WhereExpressionInfo<T>(
                    Expression.Lambda<Func<T, bool>>(binaryExpresioFilter, parameter)));
        }

        return new OrderedSpecificationBuilder<T>(specificationBuilder.Specification);
    }

    /// <summary>
    /// Build filter expression từ Logic và Filters (recursive).
    /// </summary>
    private static Expression CreateFilterExpression(
        string logic,
        IEnumerable<Filter> filters,
        ParameterExpression parameter)
    {
        Expression filterExpression = default!;

        foreach (var filter in filters)
        {
            Expression bExpresionFilter;

            if (!string.IsNullOrEmpty(filter.Logic))
            {
                if (filter.Filters is null)
                {
                    throw new FluentValidation.ValidationException("The Filters attribute is required when declaring a logic");
                }

                bExpresionFilter = CreateFilterExpression(filter.Logic, filter.Filters, parameter);
            }
            else
            {
                var filterValid = GetValidFilter(filter);
                bExpresionFilter = CreateFilterExpression(
                    filterValid.Field!,
                    filterValid.Operator!,
                    filterValid.Value,
                    parameter);
            }

            filterExpression = filterExpression is null
                ? bExpresionFilter
                : CombineFilter(logic, filterExpression, bExpresionFilter);
        }

        return filterExpression;
    }

    /// <summary>
    /// Build filter expression từ Field, Operator, Value.
    /// </summary>
    private static Expression CreateFilterExpression(
        string field,
        string filterOperator,
        object? value,
        ParameterExpression parameter)
    {
        var propertyExpresion = GetPropertyExpression(field, parameter);
        var valueExpresion = GeValuetExpression(field, value, propertyExpresion.Type);
        return CreateFilterExpression(propertyExpresion, valueExpresion, filterOperator);
    }

    /// <summary>
    /// Build binary expression từ member expression và constant expression.
    /// </summary>
    private static Expression CreateFilterExpression(
        Expression memberExpression,
        Expression constantExpression,
        string filterOperator)
    {
        bool stringMember = memberExpression.Type == typeof(string);
        if (constantExpression is ConstantExpression { Value: null })
        {
            return filterOperator switch
            {
                FilterOperator.EQ => Expression.Equal(memberExpression, constantExpression),
                FilterOperator.NEQ => Expression.NotEqual(memberExpression, constantExpression),
                _ => throw new FluentValidation.ValidationException("Null supports only eq/neq filters.")
            };
        }

        if (stringMember && filterOperator is not (FilterOperator.EQ or FilterOperator.NEQ or FilterOperator.CONTAINS or FilterOperator.STARTSWITH or FilterOperator.ENDSWITH))
        {
            throw new FluentValidation.ValidationException("Unsupported string filter operator.");
        }

        if (!stringMember && filterOperator is FilterOperator.CONTAINS or FilterOperator.STARTSWITH or FilterOperator.ENDSWITH)
        {
            throw new FluentValidation.ValidationException("Text operators require a string field.");
        }

        Expression originalMember = memberExpression;
        if (stringMember)
        {
            // Keep LOWER expressions: StringComparison overloads are not portable EF SQL translations.
            constantExpression = Expression.Call(constantExpression, nameof(string.ToLower), null);
            memberExpression = Expression.Call(memberExpression, nameof(string.ToLower), null);
        }

        Expression comparison;
        try
        {
            comparison = filterOperator switch
            {
                FilterOperator.EQ => Expression.Equal(memberExpression, constantExpression),
                FilterOperator.NEQ => Expression.NotEqual(memberExpression, constantExpression),
                FilterOperator.LT => Expression.LessThan(memberExpression, constantExpression),
                FilterOperator.LTE => Expression.LessThanOrEqual(memberExpression, constantExpression),
                FilterOperator.GT => Expression.GreaterThan(memberExpression, constantExpression),
                FilterOperator.GTE => Expression.GreaterThanOrEqual(memberExpression, constantExpression),
                FilterOperator.CONTAINS => Expression.Call(memberExpression, nameof(string.Contains), null, constantExpression),
                FilterOperator.STARTSWITH => Expression.Call(memberExpression, nameof(string.StartsWith), null, constantExpression),
                FilterOperator.ENDSWITH => Expression.Call(memberExpression, nameof(string.EndsWith), null, constantExpression),
                _ => throw new FluentValidation.ValidationException("Invalid filter operator.")
            };
        }
        catch (InvalidOperationException)
        {
            throw new FluentValidation.ValidationException("The operator is not supported for this field type.");
        }

        if (!stringMember)
        {
            return comparison;
        }

        Expression isNull = Expression.Equal(originalMember, Expression.Constant(null, typeof(string)));
        return filterOperator == FilterOperator.NEQ
            ? Expression.OrElse(isNull, comparison)
            : Expression.AndAlso(Expression.Not(isNull), comparison);
    }

    /// <summary>
    /// Kết hợp hai expressions với logic operator.
    /// </summary>
    private static Expression CombineFilter(
        string filterOperator,
        Expression bExpresionBase,
        Expression bExpresion) => filterOperator switch
        {
            FilterLogic.AND => Expression.AndAlso(bExpresionBase, bExpresion),
            FilterLogic.OR => Expression.OrElse(bExpresionBase, bExpresion),
            FilterLogic.XOR => Expression.ExclusiveOr(bExpresionBase, bExpresion),
            _ => throw new ArgumentException("FilterLogic is not valid."),
        };

    #endregion

    #region OrderBy Methods

    /// <summary>
    /// Extension method để apply ordering vào specification.
    /// </summary>
    public static IOrderedSpecificationBuilder<T> OrderBy<T>(
        this ISpecificationBuilder<T> specificationBuilder,
        string[]? orderByFields)
    {
        if (orderByFields?.Length > 8)
        {
            throw new FluentValidation.ValidationException("At most eight ordering fields are allowed.");
        }

        // Paging requires a total order: equal labels/prices must not move between pages.
        var fields = ParseOrderBy(orderByFields ?? Array.Empty<string>());
        if (!fields.ContainsKey("Id"))
        {
            fields.Add("Id", fields.Count == 0 ? OrderTypeEnum.OrderBy : OrderTypeEnum.ThenBy);
        }

        foreach (var field in fields)
        {
            ValidateField(typeof(T), field.Key);
            var paramExpr = Expression.Parameter(typeof(T));

            Expression propertyExpr = paramExpr;
            foreach (string member in field.Key.Split('.'))
            {
                propertyExpr = Expression.PropertyOrField(propertyExpr, member);
            }

            var keySelector = Expression.Lambda<Func<T, object?>>(
                Expression.Convert(propertyExpr, typeof(object)),
                paramExpr);

            ((List<OrderExpressionInfo<T>>)specificationBuilder.Specification.OrderExpressions)
                .Add(new OrderExpressionInfo<T>(keySelector, field.Value));
        }

        return new OrderedSpecificationBuilder<T>(specificationBuilder.Specification);
    }

    /// <summary>
    /// Parse orderByFields array thành Dictionary.
    /// </summary>
    private static Dictionary<string, OrderTypeEnum> ParseOrderBy(string[] orderByFields)
    {
        var fields = new Dictionary<string, OrderTypeEnum>(StringComparer.Ordinal);
        foreach (string orderByField in orderByFields)
        {
            if (string.IsNullOrWhiteSpace(orderByField))
            {
                throw new FluentValidation.ValidationException("Ordering fields must not be empty.");
            }

            string[] fieldParts = orderByField.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string field = fieldParts[0];
            string direction = fieldParts.Length > 1 ? fieldParts[1] : "asc";
            bool descending = direction.Equals("desc", StringComparison.OrdinalIgnoreCase) || direction.Equals("descending", StringComparison.OrdinalIgnoreCase);
            bool ascending = direction.Equals("asc", StringComparison.OrdinalIgnoreCase) || direction.Equals("ascending", StringComparison.OrdinalIgnoreCase);
            if (fieldParts.Length > 2 || (!ascending && !descending) || fields.ContainsKey(field))
            {
                throw new FluentValidation.ValidationException("Ordering fields must be unique and use asc/desc directions.");
            }

            var orderBy = fields.Count == 0
                ? descending ? OrderTypeEnum.OrderByDescending : OrderTypeEnum.OrderBy
                : descending ? OrderTypeEnum.ThenByDescending : OrderTypeEnum.ThenBy;

            fields.Add(field, orderBy);
        }

        return fields;
    }

    #endregion

    private static HashSet<string> AllowedFields(Type type) => new((type.Name switch
    {
        "ApplicationUser" => "Id,UserName,Email,FirstName,LastName,PhoneNumber,IsActive,EmailConfirmed",
        "Product" => "Id,Name,Description,Slug,ProductTypeId,Status,CreatedOn",
        "ProductVariant" => "Id,ProductId,Sku,Price,CompareAtPrice,IsActive",
        "Category" => "Id,Name,Slug,Description,ParentId,SortOrder,IsActive,CreatedOn",
        "Trail" => "Id,DateTime,UserId,TableName,Type",
        _ => "Id"
    }).Split(','), StringComparer.Ordinal);
    private static void ValidateField(Type type, string field)
    {
        if (!AllowedFields(type).Contains(field)) throw new FluentValidation.ValidationException("Unknown or non-searchable field: " + field);
    }

    #region Helper Methods

    /// <summary>
    /// Build property expression từ property name (support nested properties).
    /// </summary>
    private static MemberExpression GetPropertyExpression(
        string propertyName,
        ParameterExpression parameter)
    {
        ValidateField(parameter.Type, propertyName);
        Expression propertyExpression = parameter;

        foreach (string member in propertyName.Split('.'))
        {
            propertyExpression = Expression.PropertyOrField(propertyExpression, member);
        }

        return (MemberExpression)propertyExpression;
    }

    /// <summary>
    /// Convert value thành ConstantExpression với type phù hợp.
    /// </summary>
    private static Expression GeValuetExpression(string field, object? value, Type propertyType)
    {
        try
        {
            JsonElement json = value is JsonElement element ? element : JsonSerializer.SerializeToElement(value);
            if (json.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                if (propertyType.IsValueType && Nullable.GetUnderlyingType(propertyType) is null)
                {
                    throw new FluentValidation.ValidationException($"{field} does not accept null.");
                }

                return Expression.Constant(null, propertyType);
            }

            Type type = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
            object? converted;
            if (type == typeof(DateTime))
            {
                converted = DateTimeOffset.Parse(json.GetString()!, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal).UtcDateTime;
            }
            else
            {
                converted = JsonSerializer.Deserialize(json.GetRawText(), type, FilterJsonOptions);
            }

            Expression constant = Expression.Constant(converted, type);
            return type == propertyType ? constant : Expression.Convert(constant, propertyType);
        }
        catch (Exception ex) when (ex is JsonException or FormatException or OverflowException or InvalidOperationException)
        {
            throw new FluentValidation.ValidationException($"Invalid value for filter field {field}.");
        }
    }

    private static readonly JsonSerializerOptions FilterJsonOptions = new()
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Converters = { new JsonStringEnumConverter() }
    };

    private static void ValidateFilterTree(Filter root)
    {
        var pending = new Stack<(Filter Filter, int Depth)>();
        pending.Push((root, 1));
        int count = 0;
        while (pending.TryPop(out var node))
        {
            if (++count > 100 || node.Depth > 8)
            {
                throw new FluentValidation.ValidationException("Filters are limited to 100 nodes and eight levels.");
            }

            if (!string.IsNullOrEmpty(node.Filter.Logic))
            {
                if (node.Filter.Logic is not (FilterLogic.AND or FilterLogic.OR or FilterLogic.XOR) || node.Filter.Filters is not { Count: > 0 })
                {
                    throw new FluentValidation.ValidationException("Filter groups require a valid logic and non-empty filters.");
                }

                foreach (Filter child in node.Filter.Filters)
                {
                    if (child is null)
                    {
                        throw new FluentValidation.ValidationException("A filter must not be null.");
                    }

                    pending.Push((child, node.Depth + 1));
                }
            }
        }
    }

    /// <summary>
    /// Validate Filter object.
    /// </summary>
    private static Filter GetValidFilter(Filter filter)
    {
        if (string.IsNullOrEmpty(filter.Field))
        {
            throw new FluentValidation.ValidationException("The field attribute is required when declaring a filter");
        }

        if (string.IsNullOrEmpty(filter.Operator))
        {
            throw new FluentValidation.ValidationException("The Operator attribute is required when declaring a filter");
        }

        return filter;
    }

    #endregion
}
