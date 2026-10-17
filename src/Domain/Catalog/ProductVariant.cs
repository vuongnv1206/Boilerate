using Boilerate.Domain.Common.Contracts;
using Boilerate.Domain.Common.Exceptions;
namespace Boilerate.Domain.Catalog;

public sealed class ProductVariant : BaseEntity
{
    private readonly List<VariantOptionValue> _selections = [];
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
        Sku = CatalogRules.Code(sku);
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
        Code = CatalogRules.Code(code).ToUpperInvariant();
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
        Code = CatalogRules.Code(code).ToUpperInvariant();
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