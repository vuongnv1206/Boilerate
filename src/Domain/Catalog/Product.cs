using Boilerate.Domain.Common.Contracts;
using Boilerate.Domain.Common.Exceptions;
namespace Boilerate.Domain.Catalog;

public sealed class Product : CommerceEntity, IAggregateRoot
{
    private readonly List<ProductVariant> _variants = [];
    private readonly List<ProductOption> _options = [];
    private readonly List<ProductCategory> _categories = [];
    private readonly List<ProductFieldValue> _fieldValues = [];
    private readonly List<ProductMedia> _media = [];
    public Guid ProductTypeId
    {
        get; private set;
    }
    public string Name { get; private set; } = null!;
    public string Slug { get; private set; } = null!;
    public string NormalizedSlug { get; private set; } = null!;
    public string? Description
    {
        get; private set;
    }
    public ProductStatus Status { get; private set; } = ProductStatus.Draft;
    public ProductFulfillmentKind FulfillmentKind { get; private set; } = ProductFulfillmentKind.Physical;
    public IReadOnlyCollection<ProductVariant> Variants => _variants;
    public IReadOnlyCollection<ProductOption> Options => _options;
    public IReadOnlyCollection<ProductCategory> Categories => _categories;
    public IReadOnlyCollection<ProductFieldValue> FieldValues => _fieldValues;
    public IReadOnlyCollection<ProductMedia> Media => _media;
    private Product()
    {
    }
    public Product(Guid productTypeId, string name, string slug, string? description = null, ProductFulfillmentKind fulfillmentKind = ProductFulfillmentKind.Physical)
    {
        if (productTypeId == Guid.Empty)
            throw new DomainException("ProductTypeId is required.");
        if (!Enum.IsDefined(fulfillmentKind))
            throw new DomainException("Invalid product fulfillment kind.");
        ProductTypeId = productTypeId;
        FulfillmentKind = fulfillmentKind;
        Edit(name, slug, description);
    }
    public void Edit(string name, string slug, string? description)
    {
        Name = CatalogRules.Required(name, 256, "Name");
        Slug = CatalogRules.Slug(slug);
        NormalizedSlug = Slug;
        if (description?.Length > 20000)
            throw new DomainException("Description exceeds 20000 characters.");
        Description = description;
        Touch();
    }
    public void SetCategories(IEnumerable<Guid> ids)
    {
        var selected = ids.ToHashSet();
        if (selected.Contains(Guid.Empty) || selected.Count > 100)
            throw new DomainException("Invalid categories.");
        _categories.RemoveAll(c => !selected.Contains(c.CategoryId));
        foreach (var id in selected.Where(id => _categories.All(c => c.CategoryId != id)))
            _categories.Add(new(Id, id));
        Touch();
    }
    public ProductOption AddOption(string code, string name, int maximumOptions)
    {
        if (_variants.Count != 0)
            throw new DomainException("Option axes cannot change after SKU creation; add values to an existing axis instead.");
        if (_options.Count >= maximumOptions)
            throw new DomainException("Option limit exceeded.");
        var option = new ProductOption(Id, code, name, _options.Count);
        if (_options.Any(o => o.Code == option.Code))
            throw new DomainException("Duplicate option code.");
        _options.Add(option);
        Touch();
        return option;
    }
    public ProductVariant AddVariant(string sku, decimal? price, decimal? compareAtPrice, IReadOnlyDictionary<Guid, Guid> selection, int maximumVariants)
    {
        if (_variants.Count >= maximumVariants)
            throw new DomainException("Variant limit exceeded.");
        var variant = new ProductVariant(Id, sku, price, compareAtPrice, _variants.Count == 0);
        variant.SetSelection(_options, selection);
        if (_variants.Any(v => v.CombinationKey == variant.CombinationKey || v.NormalizedSku == variant.NormalizedSku))
            throw new DomainException("Duplicate SKU or option combination.");
        _variants.Add(variant);
        Touch();
        return variant;
    }
    public void EditVariant(Guid id, decimal? price, decimal? comparison, bool active, Guid? imageMediaId)
    {
        var variant = GetVariant(id);
        if (imageMediaId.HasValue && _media.All(m => m.MediaAssetId != imageMediaId))
            throw new DomainException("Variant image must be in this product gallery.");
        if (!active && variant.IsDefault)
            throw new DomainException("Select another default SKU before deactivating this SKU.");
        variant.Edit(price, comparison, active, imageMediaId);
        Touch();
    }
    public void SelectDefault(Guid id)
    {
        var selected = GetVariant(id);
        if (!selected.IsActive)
            throw new DomainException("Default SKU must be active.");
        foreach (var variant in _variants)
            variant.SetDefault(variant.Id == id);
        Touch();
    }
    public ProductVariant GetVariant(Guid id) => _variants.SingleOrDefault(v => v.Id == id) ?? throw new DomainException("Unknown variant.");
    public void SetFields(ProductType type, IEnumerable<ProductFieldValue> values)
    {
        if (type.Id != ProductTypeId)
            throw new DomainException("Wrong product type.");
        var list = values.ToList();
        if (list.GroupBy(v => v.FieldDefinitionId).Any(g => g.Count() > 1))
            throw new DomainException("Duplicate field.");
        foreach (var value in list)
            type.ValidateValue(value, Id, _fieldValues.FirstOrDefault(v => v.FieldDefinitionId == value.FieldDefinitionId));
        _fieldValues.RemoveAll(v => list.All(n => n.FieldDefinitionId != v.FieldDefinitionId));
        foreach (var value in list)
        {
            var old = _fieldValues.SingleOrDefault(v => v.FieldDefinitionId == value.FieldDefinitionId);
            if (old is null)
                _fieldValues.Add(value);
            else
                old.CopyValue(value);
        }
        Touch();
    }
    public void SetMedia(IEnumerable<ProductMedia> media)
    {
        var list = media.ToList();
        if (list.Count > 30 || list.Any(m => m.ProductId != Id) || list.GroupBy(m => m.MediaAssetId).Any(g => g.Count() > 1))
            throw new DomainException("Invalid product gallery.");
        if (_variants.Any(v => v.ImageMediaId.HasValue && list.All(m => m.MediaAssetId != v.ImageMediaId)))
            throw new DomainException("Detach variant images before removing them from the gallery.");
        _media.RemoveAll(m => list.All(n => n.MediaAssetId != m.MediaAssetId));
        foreach (var item in list)
        {
            var old = _media.SingleOrDefault(m => m.MediaAssetId == item.MediaAssetId);
            if (old is null)
                _media.Add(item);
            else
                old.Edit(item.SortOrder, item.AltText);
        }
        Touch();
    }
    public void ApplyEditorState(IEnumerable<VariantEditorState> variants, IEnumerable<ProductMedia> media, Guid defaultVariantId)
    {
        var states = variants.ToList();
        var gallery = media.ToList();
        if (states.Count != _variants.Count || states.Select(v => v.Id).Distinct().Count() != states.Count ||
            states.Any(v => _variants.All(existing => existing.Id != v.Id)))
            throw new DomainException("Editor state must identify every SKU exactly once.");
        if (gallery.Count > 30 || gallery.Any(m => m.ProductId != Id) || gallery.Select(m => m.MediaAssetId).Distinct().Count() != gallery.Count)
            throw new DomainException("Invalid product gallery.");
        if (!states.Any(v => v.Id == defaultVariantId && v.IsActive))
            throw new DomainException("Default SKU must be active.");
        foreach (var state in states)
        {
            CatalogRules.Money(state.Price, state.CompareAtPrice);
            if (state.ImageMediaId.HasValue && gallery.All(m => m.MediaAssetId != state.ImageMediaId))
                throw new DomainException("Variant image must be in the final product gallery.");
        }
        // Validate the whole final state before changing either side of the image/default relationships.
        foreach (var state in states)
        {
            var variant = GetVariant(state.Id);
            variant.Edit(state.Price, state.CompareAtPrice, state.IsActive, state.ImageMediaId);
            variant.SetDefault(state.Id == defaultVariantId);
        }
        SetMedia(gallery);
    }
    public void ChangeStatus(ProductStatus status, ProductType type)
    {
        if (!Enum.IsDefined(status))
            throw new DomainException("Invalid product status.");
        if (status == ProductStatus.Published)
            Validate(type, true);
        Status = status;
        Touch();
    }
    public void Validate(ProductType type, bool publishing = false)
    {
        if (type.Id != ProductTypeId)
            throw new DomainException("Wrong product type.");
        if (_variants.Count == 0 || _variants.Count(v => v.IsDefault && v.IsActive) != 1)
            throw new DomainException("Exactly one active default SKU is required.");
        if (Status != ProductStatus.Published && !publishing)
            return;
        if (!type.IsActive)
            throw new DomainException("Product type is inactive.");
        if (_variants.Where(v => v.IsActive).Any(v => !v.Price.HasValue))
            throw new DomainException("Active SKUs require prices before publication.");
        if (type.Fields.Any(f => f.IsActive && f.IsRequired && _fieldValues.All(v => v.FieldDefinitionId != f.Id)))
            throw new DomainException("Required product fields are missing.");
    }
}
public sealed record VariantEditorState(Guid Id, decimal? Price, decimal? CompareAtPrice, bool IsActive, Guid? ImageMediaId);
public sealed class ProductCategory
{
    public Guid ProductId
    {
        get; private set;
    }
    public Guid CategoryId
    {
        get; private set;
    }
    private ProductCategory()
    {
    }
    public ProductCategory(Guid productId, Guid categoryId)
    {
        ProductId = productId;
        CategoryId = categoryId;
    }
}
public sealed class ProductMedia
{
    public Guid ProductId
    {
        get; private set;
    }
    public Guid MediaAssetId
    {
        get; private set;
    }
    public int SortOrder
    {
        get; private set;
    }
    public string? AltText
    {
        get; private set;
    }
    private ProductMedia()
    {
    }
    public ProductMedia(Guid productId, Guid mediaAssetId, int sortOrder, string? altText)
    {
        ProductId = productId;
        MediaAssetId = mediaAssetId;
        Edit(sortOrder, altText);
    }
    public void Edit(int order, string? alt)
    {
        if (order < 0 || alt?.Length > 500)
            throw new DomainException("Invalid media presentation.");
        SortOrder = order;
        AltText = alt;
    }
}
