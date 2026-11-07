using Boilerate.Application.Common.Persistence;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Catalog.Products;

internal static class ProductSearchHelper
{
    internal static async Task<Page<ProductSummary>> SearchAsync(
        IApplicationDbContext db,
        ProductSearch filter,
        bool publishedOnly,
        CancellationToken ct)
    {
        CommerceConcurrency.ValidatePage(filter.PageNumber, filter.PageSize);
        if (filter.Fields is null ||
            filter.OptionValueIds is null ||
            filter.Fields.Count > 20 ||
            filter.Fields.Any(f => f is null) ||
            filter.OptionValueIds.Count > 10 ||
            filter.Search?.Length > 200 ||
            filter.MinimumPrice < 0 ||
            filter.MaximumPrice < 0 ||
            (filter.MinimumPrice.HasValue && filter.MaximumPrice < filter.MinimumPrice) ||
            (filter.Status.HasValue && !Enum.IsDefined(filter.Status.Value)) ||
            (filter.FulfillmentKind.HasValue && !Enum.IsDefined(filter.FulfillmentKind.Value)) ||
            filter.SortBy is not ("modified" or "name" or "price") ||
            filter.SortDirection is not ("asc" or "desc"))
        {
            throw new DomainException("Invalid filters.");
        }

        CatalogRules.Money(filter.MinimumPrice, null);
        CatalogRules.Money(filter.MaximumPrice, null);

        var q = db.Products.AsNoTracking().Where(p => !p.IsDeleted);
        if (publishedOnly)
        {
            q = q.Where(p => p.Status == ProductStatus.Published);
        }
        else if (filter.Status.HasValue)
        {
            q = q.Where(p => p.Status == filter.Status);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            string term = filter.Search.Trim().ToLowerInvariant();
            q = q.Where(p => p.Name.ToLower().Contains(term) || p.Slug.Contains(term));
        }

        if (filter.ProductTypeId.HasValue)
        {
            q = q.Where(p => p.ProductTypeId == filter.ProductTypeId);
        }

        if (filter.FulfillmentKind.HasValue)
        {
            q = q.Where(p => p.FulfillmentKind == filter.FulfillmentKind);
        }

        if (filter.CategoryId.HasValue)
        {
            q = q.Where(p => p.Categories.Any(c => c.CategoryId == filter.CategoryId));
        }

        if (filter.MinimumPrice.HasValue || filter.MaximumPrice.HasValue || filter.OptionValueIds.Count > 0 || filter.InStock.HasValue)
        {
            var variants = db.ProductVariants.Where(v => !v.IsRetired && v.IsActive);
            if (filter.MinimumPrice.HasValue)
            {
                variants = variants.Where(v => v.Price >= filter.MinimumPrice);
            }

            if (filter.MaximumPrice.HasValue)
            {
                variants = variants.Where(v => v.Price <= filter.MaximumPrice);
            }

            foreach (Guid id in filter.OptionValueIds)
            {
                variants = variants.Where(v => v.Selections.Any(s => s.OptionValueId == id));
            }

            q = q.Where(p => variants.Any(v => v.ProductId == p.Id));
            if (filter.InStock.HasValue)
            {
                var stockedVariants = ProductPersistenceHelper.StockedVariants(db, variants);
                q = filter.InStock.Value
                    ? q.Where(p => stockedVariants.Any(v => v.ProductId == p.Id))
                    : q.Where(p => !stockedVariants.Any(v => v.ProductId == p.Id));
            }
        }

        foreach (var fieldFilter in filter.Fields)
        {
            var definition = await db.Set<ProductFieldDefinition>()
                .AsNoTracking()
                .SingleOrDefaultAsync(f => f.Id == fieldFilter.FieldId && f.IsActive && f.IsFilterable, ct)
                ?? throw new DomainException("Unknown or non-filterable field.");
            if (filter.ProductTypeId.HasValue && definition.ProductTypeId != filter.ProductTypeId)
            {
                throw new DomainException("Field belongs to a different product type.");
            }

            var values = db.Set<ProductFieldValue>().Where(v => v.FieldDefinitionId == fieldFilter.FieldId);
            string op = fieldFilter.Operator;
            switch (definition.DataType)
            {
                case ProductFieldType.Text when op == "eq" && fieldFilter.TextValue is { Length: > 0 and <= 500 }:
                    if (fieldFilter.NumberValue.HasValue ||
                        fieldFilter.NumberTo.HasValue ||
                        fieldFilter.BooleanValue.HasValue ||
                        fieldFilter.DateValue.HasValue ||
                        fieldFilter.DateTo.HasValue ||
                        fieldFilter.ChoiceIds is not null)
                    {
                        throw new DomainException("Unexpected filter value.");
                    }

                    values = db.FilterExactText(values, fieldFilter.TextValue);
                    break;
                case ProductFieldType.Number when (op == "eq" || op == "range") && fieldFilter.NumberValue.HasValue:
                    if (fieldFilter.TextValue is not null ||
                        fieldFilter.BooleanValue.HasValue ||
                        fieldFilter.DateValue.HasValue ||
                        fieldFilter.DateTo.HasValue ||
                        fieldFilter.ChoiceIds is not null ||
                        (op == "eq" && fieldFilter.NumberTo.HasValue))
                    {
                        throw new DomainException("Unexpected filter value.");
                    }

                    if (!CatalogRules.IsFieldNumber(fieldFilter.NumberValue.Value) ||
                        (fieldFilter.NumberTo.HasValue && !CatalogRules.IsFieldNumber(fieldFilter.NumberTo.Value)))
                    {
                        throw new DomainException("Number filters must fit decimal(18,4) without rounding.");
                    }

                    if (op == "range" && (!fieldFilter.NumberTo.HasValue || fieldFilter.NumberTo < fieldFilter.NumberValue))
                    {
                        throw new DomainException("Invalid number range.");
                    }

                    values = op == "eq"
                        ? values.Where(v => v.NumberValue == fieldFilter.NumberValue)
                        : values.Where(v => v.NumberValue >= fieldFilter.NumberValue && v.NumberValue <= fieldFilter.NumberTo);
                    break;
                case ProductFieldType.Boolean when op == "eq" && fieldFilter.BooleanValue.HasValue:
                    if (fieldFilter.TextValue is not null ||
                        fieldFilter.NumberValue.HasValue ||
                        fieldFilter.NumberTo.HasValue ||
                        fieldFilter.DateValue.HasValue ||
                        fieldFilter.DateTo.HasValue ||
                        fieldFilter.ChoiceIds is not null)
                    {
                        throw new DomainException("Unexpected filter value.");
                    }

                    values = values.Where(v => v.BooleanValue == fieldFilter.BooleanValue);
                    break;
                case ProductFieldType.Date when (op == "eq" || op == "range") && fieldFilter.DateValue.HasValue:
                    if (fieldFilter.TextValue is not null ||
                        fieldFilter.NumberValue.HasValue ||
                        fieldFilter.NumberTo.HasValue ||
                        fieldFilter.BooleanValue.HasValue ||
                        fieldFilter.ChoiceIds is not null ||
                        (op == "eq" && fieldFilter.DateTo.HasValue))
                    {
                        throw new DomainException("Unexpected filter value.");
                    }

                    if (op == "range" && (!fieldFilter.DateTo.HasValue || fieldFilter.DateTo < fieldFilter.DateValue))
                    {
                        throw new DomainException("Invalid date range.");
                    }

                    values = op == "eq"
                        ? values.Where(v => v.DateValue == fieldFilter.DateValue)
                        : values.Where(v => v.DateValue >= fieldFilter.DateValue && v.DateValue <= fieldFilter.DateTo);
                    break;
                case ProductFieldType.Choice when op == "in" && fieldFilter.ChoiceIds is { Count: > 0 and <= 100 }:
                    if (fieldFilter.TextValue is not null ||
                        fieldFilter.NumberValue.HasValue ||
                        fieldFilter.NumberTo.HasValue ||
                        fieldFilter.BooleanValue.HasValue ||
                        fieldFilter.DateValue.HasValue ||
                        fieldFilter.DateTo.HasValue)
                    {
                        throw new DomainException("Unexpected filter value.");
                    }

                    var choices = fieldFilter.ChoiceIds.Distinct().ToArray();
                    if (await db.Set<ProductFieldChoice>()
                            .CountAsync(c => c.FieldDefinitionId == definition.Id && c.IsActive && choices.Contains(c.Id), ct) != choices.Length)
                    {
                        throw new DomainException("Invalid filter choices.");
                    }

                    values = values.Where(v => v.ChoiceId.HasValue && choices.Contains(v.ChoiceId.Value));
                    break;
                default:
                    throw new DomainException("Operator or value does not match the field type.");
            }

            q = q.Where(p => values.Any(v => v.ProductId == p.Id));
        }

        int total = await q.CountAsync(ct);
        var availableVariantIds = ProductPersistenceHelper.StockedVariants(
                db,
                db.ProductVariants.Where(v => !v.IsRetired && v.IsActive))
            .Select(v => v.Id);
        var ordered = (filter.SortBy, filter.SortDirection) switch
        {
            ("name", "asc") => q.OrderBy(p => p.Name),
            ("name", "desc") => q.OrderByDescending(p => p.Name),
            ("price", "asc") => q.OrderBy(p => !p.Variants.Any(v => v.IsActive && v.Price.HasValue))
                .ThenBy(p => p.Variants.Where(v => v.IsActive).Min(v => v.Price)),
            ("price", "desc") => q.OrderBy(p => !p.Variants.Any(v => v.IsActive && v.Price.HasValue))
                .ThenByDescending(p => p.Variants.Where(v => v.IsActive).Min(v => v.Price)),
            ("modified", "asc") => q.OrderBy(p => p.LastModifiedOn ?? p.CreatedOn),
            _ => q.OrderByDescending(p => p.LastModifiedOn ?? p.CreatedOn)
        };
        var items = await ordered
            .ThenBy(p => p.Id)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(p => new ProductSummary(
                p.Id,
                p.Name,
                p.Slug,
                p.Status,
                p.Variants.Where(v => v.IsActive).Min(v => v.Price),
                p.Variants.Any(v => v.IsActive && availableVariantIds.Contains(v.Id)),
                p.FulfillmentKind,
                p.ProductTypeId,
                db.ProductTypes.Where(t => t.Id == p.ProductTypeId).Select(t => t.Name).First(),
                p.Variants.Where(v => v.IsDefault).Select(v => v.ImageMediaId).FirstOrDefault() ??
                    p.Media.OrderBy(m => m.SortOrder).ThenBy(m => m.MediaAssetId).Select(m => (Guid?)m.MediaAssetId).FirstOrDefault(),
                p.Version,
                CommerceConcurrency.UtcTimestamp(p.LastModifiedOn ?? p.CreatedOn)))
            .ToListAsync(ct);
        return new Page<ProductSummary>(items, total, filter.PageNumber, filter.PageSize);
    }
}

