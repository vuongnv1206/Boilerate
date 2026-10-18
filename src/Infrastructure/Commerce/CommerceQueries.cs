using Boilerate.Application.Catalog;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using Boilerate.Domain.Inventory;
using Boilerate.Domain.DigitalInventory;
using Microsoft.EntityFrameworkCore;
namespace Boilerate.Infrastructure.Commerce;

public sealed partial class CommerceService
{
    private IQueryable<ProductVariant> StockedVariants(IQueryable<ProductVariant> variants) => variants.Where(v =>
        db.Products.Any(p => p.Id == v.ProductId &&
            ((p.FulfillmentKind == ProductFulfillmentKind.Physical && db.InventoryItems.Any(i => i.VariantId == v.Id && i.OnHand > i.Reserved)) ||
             ((p.FulfillmentKind == ProductFulfillmentKind.DigitalUnique || p.FulfillmentKind == ProductFulfillmentKind.DigitalShared) &&
                db.DigitalStockItems.Any(i => i.VariantId == v.Id && i.State == DigitalStockState.Available)) ||
             p.FulfillmentKind == ProductFulfillmentKind.ManualExtension)));

    public async Task<ProductDto> GetAsync(Guid id, bool publishedOnly, CancellationToken ct)
    {
        var p = await ProductGraph().AsNoTracking().SingleOrDefaultAsync(p => p.Id == id && (!publishedOnly || p.Status == ProductStatus.Published), ct)
            ?? throw new NotFoundException("Product not found.");
        var ids = p.Variants.Select(v => v.Id).ToArray();
        var stocked = await StockedVariants(db.ProductVariants.Where(v => ids.Contains(v.Id) && v.IsActive)).Select(v => v.Id).ToListAsync(ct);
        var fields = p.FieldValues.ToArray();
        var categories = p.Categories.Select(c => c.CategoryId).ToArray();
        if (publishedOnly)
        {
            var activeFields = await db.Set<ProductFieldDefinition>().Where(f => f.ProductTypeId == p.ProductTypeId && f.IsActive).Select(f => f.Id).ToListAsync(ct);
            fields = fields.Where(f => activeFields.Contains(f.FieldDefinitionId)).ToArray();
            categories = await db.Categories.Where(c => categories.Contains(c.Id) && c.IsActive).Select(c => c.Id).ToArrayAsync(ct);
        }
        var definitions = await db.Set<ProductFieldDefinition>().AsNoTracking().Where(f => f.ProductTypeId == p.ProductTypeId).Include(f => f.Choices).ToListAsync(ct);
        return new(p.Id,p.ProductTypeId,p.Name,p.Slug,p.Description,p.Status,p.Version,categories,
            p.Options.OrderBy(o => o.SortOrder).ThenBy(o => o.Id).Select(o => new OptionDto(o.Id,o.Code,o.Name,o.SortOrder,
                o.Values.OrderBy(v => v.SortOrder).ThenBy(v => v.Id).Select(v => new OptionValueDto(v.Id,v.Code,v.Label,v.SortOrder)).ToArray())).ToArray(),
            p.Variants.Where(v => !publishedOnly || v.IsActive).OrderByDescending(v => v.IsDefault).ThenBy(v => v.Id)
                .Select(v => new VariantDto(v.Id,v.Sku,v.CombinationKey,v.Price,v.CompareAtPrice,v.Currency,v.IsDefault,v.IsActive,v.ImageMediaId,stocked.Contains(v.Id),
                    v.Selections.Select(s => new VariantSelectionDto(s.OptionId,s.OptionValueId)).ToArray())).ToArray(),
            fields.Select(f =>
            {
                var definition = definitions.Single(d => d.Id == f.FieldDefinitionId);
                return new FieldValueDto(f.FieldDefinitionId,f.FieldType,definition.Label,f.TextValue,f.NumberValue,f.BooleanValue,f.DateValue,f.ChoiceId,
                    definition.Choices.SingleOrDefault(c => c.Id == f.ChoiceId)?.Label);
            }).ToArray(),
            p.Media.OrderBy(m => m.SortOrder).ThenBy(m => m.MediaAssetId).Select(m => new MediaInput(m.MediaAssetId,m.SortOrder,m.AltText)).ToArray(), p.FulfillmentKind);
    }
    public async Task<ProductDto> GetBySlugAsync(string slug, CancellationToken ct)
    {
        slug = CatalogRules.Slug(slug);
        var id = await db.Products.Where(p => p.NormalizedSlug == slug && p.Status == ProductStatus.Published).Select(p => (Guid?)p.Id).SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("Product not found.");
        return await GetAsync(id, true, ct);
    }
    internal static void ValidatePage(int page, int size)
    {
        if (page < 1 || size is < 1 or > 100 || (long)(page - 1) * size > int.MaxValue)
            throw new DomainException("Invalid pagination; maximum page size is 100.");
    }
    public async Task<Page<ProductSummary>> SearchAsync(ProductSearch r, bool publishedOnly, CancellationToken ct)
    {
        ValidatePage(r.PageNumber, r.PageSize);
        if (r.Fields is null || r.OptionValueIds is null || r.Fields.Count > 20 || r.Fields.Any(f => f is null) || r.OptionValueIds.Count > 10 || r.Search?.Length > 200 ||
            r.MinimumPrice < 0 || r.MaximumPrice < 0 || (r.MinimumPrice.HasValue && r.MaximumPrice < r.MinimumPrice) || (r.Status.HasValue && !Enum.IsDefined(r.Status.Value)))
            throw new DomainException("Invalid filters.");
        CatalogRules.Money(r.MinimumPrice, null);
        CatalogRules.Money(r.MaximumPrice, null);
        var q = db.Products.AsNoTracking();
        if (publishedOnly)
            q = q.Where(p => p.Status == ProductStatus.Published);
        else if (r.Status.HasValue)
            q = q.Where(p => p.Status == r.Status);
        if (!string.IsNullOrWhiteSpace(r.Search))
        {
            var term = r.Search.Trim().ToLowerInvariant();
            q = q.Where(p => p.Name.ToLower().Contains(term) || p.Slug.Contains(term));
        }
        if (r.ProductTypeId.HasValue)
            q = q.Where(p => p.ProductTypeId == r.ProductTypeId);
        if (r.CategoryId.HasValue)
            q = q.Where(p => p.Categories.Any(c => c.CategoryId == r.CategoryId));
        if (r.MinimumPrice.HasValue || r.MaximumPrice.HasValue || r.OptionValueIds.Count > 0 || r.InStock.HasValue)
        {
            var variants = db.ProductVariants.Where(v => v.IsActive);
            if (r.MinimumPrice.HasValue)
                variants = variants.Where(v => v.Price >= r.MinimumPrice);
            if (r.MaximumPrice.HasValue)
                variants = variants.Where(v => v.Price <= r.MaximumPrice);
            foreach (var id in r.OptionValueIds)
                variants = variants.Where(v => v.Selections.Any(s => s.OptionValueId == id));
            q = q.Where(p => variants.Any(v => v.ProductId == p.Id));
            if (r.InStock.HasValue)
            {
                var stockedVariants = StockedVariants(variants);
                q = r.InStock.Value ? q.Where(p => stockedVariants.Any(v => v.ProductId == p.Id)) : q.Where(p => !stockedVariants.Any(v => v.ProductId == p.Id));
            }
        }
        foreach (var filter in r.Fields)
        {
            var definition = await db.Set<ProductFieldDefinition>().AsNoTracking().SingleOrDefaultAsync(f => f.Id == filter.FieldId && f.IsActive && f.IsFilterable, ct)
                ?? throw new DomainException("Unknown or non-filterable field.");
            if (r.ProductTypeId.HasValue && definition.ProductTypeId != r.ProductTypeId)
                throw new DomainException("Field belongs to a different product type.");
            var values = db.Set<ProductFieldValue>().Where(v => v.FieldDefinitionId == filter.FieldId);
            var op = filter.Operator;
            switch (definition.DataType)
            {
                case ProductFieldType.Text when op == "eq" && filter.TextValue is { Length: > 0 and <= 500 }:
                    if (filter.NumberValue.HasValue || filter.NumberTo.HasValue || filter.BooleanValue.HasValue || filter.DateValue.HasValue || filter.DateTo.HasValue || filter.ChoiceIds is not null)
                        throw new DomainException("Unexpected filter value.");
                    var text = filter.TextValue;
                    // SQL Server pads strings for equality even under BIN2; byte length preserves exact trailing-space semantics.
                    values = db.Database.IsNpgsql()
                        ? values.Where(v => EF.Functions.Collate(v.TextValue!, "C") == text)
                        : values.Where(v => EF.Functions.Collate(v.TextValue!, "Latin1_General_100_BIN2") == text && EF.Functions.DataLength(v.TextValue) == text.Length * 2);
                    break;
                case ProductFieldType.Number when (op == "eq" || op == "range") && filter.NumberValue.HasValue:
                    if (filter.TextValue is not null || filter.BooleanValue.HasValue || filter.DateValue.HasValue || filter.DateTo.HasValue || filter.ChoiceIds is not null || (op == "eq" && filter.NumberTo.HasValue))
                        throw new DomainException("Unexpected filter value.");
                    if (!CatalogRules.IsFieldNumber(filter.NumberValue.Value) || (filter.NumberTo.HasValue && !CatalogRules.IsFieldNumber(filter.NumberTo.Value)))
                        throw new DomainException("Number filters must fit decimal(18,4) without rounding.");
                    if (op == "range" && (!filter.NumberTo.HasValue || filter.NumberTo < filter.NumberValue))
                        throw new DomainException("Invalid number range.");
                    values = op == "eq" ? values.Where(v => v.NumberValue == filter.NumberValue) : values.Where(v => v.NumberValue >= filter.NumberValue && v.NumberValue <= filter.NumberTo);
                    break;
                case ProductFieldType.Boolean when op == "eq" && filter.BooleanValue.HasValue:
                    if (filter.TextValue is not null || filter.NumberValue.HasValue || filter.NumberTo.HasValue || filter.DateValue.HasValue || filter.DateTo.HasValue || filter.ChoiceIds is not null)
                        throw new DomainException("Unexpected filter value.");
                    values = values.Where(v => v.BooleanValue == filter.BooleanValue);
                    break;
                case ProductFieldType.Date when (op == "eq" || op == "range") && filter.DateValue.HasValue:
                    if (filter.TextValue is not null || filter.NumberValue.HasValue || filter.NumberTo.HasValue || filter.BooleanValue.HasValue || filter.ChoiceIds is not null || (op == "eq" && filter.DateTo.HasValue))
                        throw new DomainException("Unexpected filter value.");
                    if (op == "range" && (!filter.DateTo.HasValue || filter.DateTo < filter.DateValue))
                        throw new DomainException("Invalid date range.");
                    values = op == "eq" ? values.Where(v => v.DateValue == filter.DateValue) : values.Where(v => v.DateValue >= filter.DateValue && v.DateValue <= filter.DateTo);
                    break;
                case ProductFieldType.Choice when op == "in" && filter.ChoiceIds is { Count: > 0 and <= 100 }:
                    if (filter.TextValue is not null || filter.NumberValue.HasValue || filter.NumberTo.HasValue || filter.BooleanValue.HasValue || filter.DateValue.HasValue || filter.DateTo.HasValue)
                        throw new DomainException("Unexpected filter value.");
                    var choices = filter.ChoiceIds.Distinct().ToArray();
                    if (await db.Set<ProductFieldChoice>().CountAsync(c => c.FieldDefinitionId == definition.Id && c.IsActive && choices.Contains(c.Id), ct) != choices.Length)
                        throw new DomainException("Invalid filter choices.");
                    values = values.Where(v => v.ChoiceId.HasValue && choices.Contains(v.ChoiceId.Value));
                    break;
                default:
                    throw new DomainException("Operator or value does not match the field type.");
            }
            q = q.Where(p => values.Any(v => v.ProductId == p.Id));
        }
        int total = await q.CountAsync(ct);
        var availableVariantIds = StockedVariants(db.ProductVariants.Where(v => v.IsActive)).Select(v => v.Id);
        var items = await q.OrderBy(p => p.Id).Skip((r.PageNumber - 1) * r.PageSize).Take(r.PageSize)
            .Select(p => new ProductSummary(p.Id, p.Name, p.Slug, p.Status, p.Variants.Where(v => v.IsActive).Min(v => v.Price),
                p.Variants.Any(v => v.IsActive && availableVariantIds.Contains(v.Id)), p.FulfillmentKind)).ToListAsync(ct);
        return new(items, total, r.PageNumber, r.PageSize);
    }
    public Task<List<Category>> CategoriesAsync(bool publicOnly, CancellationToken ct) => db.Categories.AsNoTracking().Where(c => !publicOnly || c.IsActive).OrderBy(c => c.SortOrder).ThenBy(c => c.Id).ToListAsync(ct);
    public Task<List<ProductType>> TypesAsync(CancellationToken ct) => db.ProductTypes.AsNoTracking().OrderBy(t => t.Id).ToListAsync(ct);
    public async Task<ProductType> TypeAsync(Guid id, CancellationToken ct) => await db.ProductTypes.AsNoTracking().Include(t => t.Fields).ThenInclude(f => f.Choices).AsSplitQuery().SingleOrDefaultAsync(t => t.Id == id, ct) ?? throw new NotFoundException("Product type not found.");
    public async Task<Page<InventoryDto>> InventoryAsync(int page, int size, CancellationToken ct)
    {
        ValidatePage(page, size);
        var q = db.InventoryItems.AsNoTracking();
        return new(await q.OrderBy(i => i.Id).Skip((page - 1) * size).Take(size).Select(i => new InventoryDto(i.Id, i.VariantId, i.LocationId, i.OnHand, i.Version, i.Reserved)).ToListAsync(ct), await q.CountAsync(ct), page, size);
    }
    internal static MovementDto MapMovement(StockMovement m) => new(m.Id, m.InventoryItemId, m.OperationId, m.Delta, m.QuantityAfter, m.Reason, m.ActorId, m.CreatedAt);
    public async Task<Page<MovementDto>> MovementsAsync(Guid itemId, int page, int size, CancellationToken ct)
    {
        ValidatePage(page, size);
        var q = db.StockMovements.AsNoTracking().Where(m => m.InventoryItemId == itemId);
        var rows = await q.OrderByDescending(m => m.CreatedAt).ThenBy(m => m.Id).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new(rows.Select(MapMovement).ToList(), await q.CountAsync(ct), page, size);
    }
}
