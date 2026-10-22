using Boilerate.Domain.Common.Contracts;
using Boilerate.Domain.Common.Exceptions;
namespace Boilerate.Domain.Catalog;

public sealed class ProductVariant : BaseEntity
{
    private readonly List<VariantOptionValue> _selections = [];
    private readonly List<VariantMedia> _images = [];
    public bool IsRetired { get; private set; }
    public bool TrackInventory { get; private set; } = true;
    public decimal? Weight { get; private set; }
    public decimal? Width { get; private set; }
    public decimal? Height { get; private set; }
    public decimal? Length { get; private set; }
    public IReadOnlyCollection<VariantMedia> Images => _images;
    public Guid ProductId
    {
        get; private set;
    }
    public string Sku { get; private set; } = null!;
    public string NormalizedSku { get; private set; } = null!;
    public string CombinationKey { get; private set; } = "DEFAULT";
    public decimal? Price
    {
        get; private set;
    }
    public decimal? CompareAtPrice
    {
        get; private set;
    }
    public string Currency { get; private set; } = "VND";
    public bool IsDefault
    {
        get; private set;
    }
    public bool IsActive { get; private set; } = true;
    public Guid? ImageMediaId
    {
        get; private set;
    }
    public IReadOnlyCollection<VariantOptionValue> Selections => _selections;
    private ProductVariant()
    {
    }
    internal ProductVariant(Guid productId, string sku, decimal? price, decimal? comparison, bool isDefault)
    {
        ProductId = productId;
        Sku = (sku ?? "").Trim();
        if (Sku.Length > 100) throw new DomainException("SKU must not exceed 100 characters.");
        NormalizedSku = Sku.ToUpperInvariant();
        Edit(price, comparison, true, null);
        IsDefault = isDefault;
    }
    internal void SetSelection(IReadOnlyCollection<ProductOption> options, IReadOnlyDictionary<Guid, Guid> selection)
    {
        if (options.Count != selection.Count)
            throw new DomainException("A SKU must select exactly one value from every option.");
        foreach (var option in options)
        {
            if (!selection.TryGetValue(option.Id, out var id) || option.Values.All(v => v.Id != id))
                throw new DomainException("Invalid option value.");
            _selections.Add(new(ProductId, Id, option.Id, id));
        }
        CombinationKey = options.Count == 0 ? "DEFAULT" : string.Join(";", selection.OrderBy(p => p.Key).Select(p => $"{p.Key:N}={p.Value:N}"));
    }
    internal void AddSelection(ProductOption option, ProductOptionValue value)
    {
        if (value.OptionId != option.Id || _selections.Any(s => s.OptionId == option.Id))
            throw new DomainException("Invalid option value assignment.");
        _selections.Add(new(ProductId, Id, option.Id, value.Id));
        CombinationKey = string.Join(";", _selections.OrderBy(s => s.OptionId).Select(s => $"{s.OptionId:N}={s.OptionValueId:N}"));
    }
    public void Configure(string? sku, bool trackInventory, decimal? weight, decimal? width, decimal? height, decimal? length, IEnumerable<VariantMedia> images)
    {
        var text = (sku ?? "").Trim();
        if (text.Length > 100) throw new DomainException("SKU must not exceed 100 characters.");
        if (new[] { weight, width, height, length }.Any(v => v is < 0 or > 999999999m))
            throw new DomainException("Measurements must be nonnegative numbers.");
        var list = images.ToList();
        if (list.Count > 30 || list.Select(m => m.MediaAssetId).Distinct().Count() != list.Count)
            throw new DomainException("Invalid variant images.");
        Sku = text; NormalizedSku = text.ToUpperInvariant(); TrackInventory = trackInventory;
        Weight = weight; Width = width; Height = height; Length = length;
        _images.RemoveAll(m => list.All(n => n.MediaAssetId != m.MediaAssetId));
        foreach (var image in list)
        {
            var current = _images.SingleOrDefault(m => m.MediaAssetId == image.MediaAssetId);
            if (current is null) _images.Add(image); else current.Edit(image.SortOrder, image.AltText);
        }
    }
    public void ReplaceSelection(IReadOnlyCollection<ProductOption> options, IReadOnlyDictionary<Guid, Guid> selections)
    {
        if (options.Count != selections.Count || options.Any(o => !selections.TryGetValue(o.Id, out var id) || o.Values.All(v => v.Id != id || v.IsRetired)))
            throw new DomainException("A variant must select one value from every option.");
        _selections.Clear();
        SetSelection(options, selections);
    }
    public void Retire() { IsRetired = true; IsActive = false; IsDefault = false; ImageMediaId = null; }
    internal void SetDefault(bool value) => IsDefault = value;
    internal void Edit(decimal? price, decimal? comparison, bool active, Guid? mediaId)
    {
        CatalogRules.Money(price, comparison);
        Price = price;
        CompareAtPrice = comparison;
        IsActive = active;
        ImageMediaId = mediaId;
    }
}
public sealed class ProductOption : BaseEntity
{
    private readonly List<ProductOptionValue> _values = [];
    public Guid ProductId
    {
        get; private set;
    }
    public bool IsRetired { get; private set; }
    public void Retire() => IsRetired = true;
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public int SortOrder
    {
        get; private set;
    }
    public IReadOnlyCollection<ProductOptionValue> Values => _values;
    private ProductOption()
    {
    }
    internal ProductOption(Guid productId, string code, string name, int sortOrder)
    {
        ProductId = productId;
        Code = CatalogRules.InternalCode(code, "OPT").ToUpperInvariant();
        Rename(name, sortOrder);
    }
    public void Rename(string name, int sortOrder)
    {
        if (sortOrder < 0)
            throw new DomainException("Negative sort order.");
        Name = CatalogRules.Required(name, 100, "Option name");
        SortOrder = sortOrder;
    }
    public ProductOptionValue AddValue(string code, string label)
    {
        var value = new ProductOptionValue(Id, code, label, _values.Count);
        if (_values.Count >= 200 || _values.Any(v => v.Code == value.Code))
            throw new DomainException("Duplicate value or value limit exceeded.");
        _values.Add(value);
        return value;
    }
}
public sealed class ProductOptionValue : BaseEntity
{
    public Guid OptionId
    {
        get; private set;
    }
    public bool IsRetired { get; private set; }
    public void Retire() => IsRetired = true;
    public string Code { get; private set; } = null!;
    public string Label { get; private set; } = null!;
    public int SortOrder
    {
        get; private set;
    }
    private ProductOptionValue()
    {
    }
    internal ProductOptionValue(Guid optionId, string code, string label, int order)
    {
        OptionId = optionId;
        Code = CatalogRules.InternalCode(code, "OPT").ToUpperInvariant();
        Rename(label, order);
    }
    public void Rename(string label, int order)
    {
        if (order < 0)
            throw new DomainException("Negative sort order.");
        Label = CatalogRules.Required(label, 100, "Value label");
        SortOrder = order;
    }
}
public sealed class VariantOptionValue
{
    public Guid ProductId
    {
        get; private set;
    }
    public Guid VariantId
    {
        get; private set;
    }
    public Guid OptionId
    {
        get; private set;
    }
    public Guid OptionValueId
    {
        get; private set;
    }
    private VariantOptionValue()
    {
    }
    internal VariantOptionValue(Guid productId, Guid variantId, Guid optionId, Guid valueId)
    {
        ProductId = productId;
        VariantId = variantId;
        OptionId = optionId;
        OptionValueId = valueId;
    }
}

public sealed class VariantMedia
{
    public Guid VariantId { get; private set; }
    public Guid MediaAssetId { get; private set; }
    public int SortOrder { get; private set; }
    public string? AltText { get; private set; }
    private VariantMedia() { }
    public VariantMedia(Guid variantId, Guid mediaAssetId, int sortOrder, string? altText)
    { VariantId = variantId; MediaAssetId = mediaAssetId; Edit(sortOrder, altText); }
    public void Edit(int sortOrder, string? altText)
    { if (sortOrder < 0 || altText?.Length > 500) throw new DomainException("Invalid image presentation."); SortOrder = sortOrder; AltText = altText; }
}
