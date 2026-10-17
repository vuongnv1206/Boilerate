using Boilerate.Domain.Common.Contracts;
using Boilerate.Domain.Common.Exceptions;
namespace Boilerate.Domain.Catalog;

public sealed class ProductType : CommerceEntity, IAggregateRoot
{
    private readonly List<ProductFieldDefinition> _fields = [];
    public string Code { get; private set; } = null!;
    public string NormalizedCode { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public bool IsActive { get; private set; } = true;
    public IReadOnlyCollection<ProductFieldDefinition> Fields => _fields;
    private ProductType()
    {
    }
    public ProductType(string code, string name)
    {
        Code = CatalogRules.Code(code);
        NormalizedCode = Code.ToUpperInvariant();
        Edit(name, true);
    }
    public void Edit(string name, bool active)
    {
        Name = CatalogRules.Required(name, 256, "Name");
        IsActive = active;
        Touch();
    }
    public ProductFieldDefinition AddField(string code, ProductFieldType type, string label, string? help, bool required, bool filterable, bool hasProducts)
    {
        if (hasProducts && required)
            throw new DomainException("Only optional fields can be added after products exist.");
        if (_fields.Count >= 100)
            throw new DomainException("Field limit exceeded.");
        var field = new ProductFieldDefinition(Id, code, type, label, help, required, filterable, _fields.Count);
        if (_fields.Any(f => f.Code == field.Code))
            throw new DomainException("Duplicate field code.");
        _fields.Add(field);
        Touch();
        return field;
    }
    public void ValidateValue(ProductFieldValue value, Guid productId, ProductFieldValue? previous)
    {
        var field = _fields.SingleOrDefault(f => f.Id == value.FieldDefinitionId);
        if (value.ProductId != productId || value.ProductTypeId != Id || field is null || field.DataType != value.FieldType)
            throw new DomainException("Field does not belong to this product type or has the wrong data type.");
        value.ValidateShape();
        if (!field.IsActive && (previous is null || !previous.SameValue(value)))
            throw new DomainException("Inactive field cannot receive new values.");
        if (value.ChoiceId.HasValue)
        {
            var choice = field.Choices.SingleOrDefault(c => c.Id == value.ChoiceId);
            if (choice is null || (!choice.IsActive && previous?.ChoiceId != choice.Id))
                throw new DomainException("Invalid or inactive choice.");
        }
    }
}
public sealed class ProductFieldDefinition : BaseEntity
{
    private readonly List<ProductFieldChoice> _choices = [];
    public Guid ProductTypeId
    {
        get; private set;
    }
    public string Code { get; private set; } = null!;
    public ProductFieldType DataType
    {
        get; private set;
    }
    public string Label { get; private set; } = null!;
    public string? HelpText
    {
        get; private set;
    }
    public bool IsRequired
    {
        get; private set;
    }
    public bool IsFilterable
    {
        get; private set;
    }
    public bool IsActive { get; private set; } = true;
    public int SortOrder
    {
        get; private set;
    }
    public IReadOnlyCollection<ProductFieldChoice> Choices => _choices;
    private ProductFieldDefinition()
    {
    }
    internal ProductFieldDefinition(Guid typeId, string code, ProductFieldType type, string label, string? help, bool required, bool filterable, int order)
    {
        if (!Enum.IsDefined(type))
            throw new DomainException("Unknown field type.");
        ProductTypeId = typeId;
        Code = CatalogRules.Code(code).ToUpperInvariant();
        DataType = type;
        IsRequired = required;
        Edit(label, help, filterable, true, order);
    }
    public void Edit(string label, string? help, bool filterable, bool active, int order)
    {
        if (help?.Length > 1000 || order < 0)
            throw new DomainException("Invalid field presentation.");
        Label = CatalogRules.Required(label, 256, "Label");
        HelpText = help;
        IsFilterable = filterable;
        IsActive = active;
        SortOrder = order;
    }
    public ProductFieldChoice AddChoice(string code, string label)
    {
        if (DataType != ProductFieldType.Choice || _choices.Count >= 200)
            throw new DomainException("Choices require a single-select field, maximum 200 choices.");
        var choice = new ProductFieldChoice(Id, code, label, _choices.Count);
        if (_choices.Any(c => c.Code == choice.Code))
            throw new DomainException("Duplicate choice code.");
        _choices.Add(choice);
        return choice;
    }
}
public sealed class ProductFieldChoice : BaseEntity
{
    public Guid FieldDefinitionId
    {
        get; private set;
    }
    public string Code { get; private set; } = null!;
    public string Label { get; private set; } = null!;
    public bool IsActive { get; private set; } = true;
    public int SortOrder
    {
        get; private set;
    }
    private ProductFieldChoice()
    {
    }
    internal ProductFieldChoice(Guid fieldId, string code, string label, int order)
    {
        FieldDefinitionId = fieldId;
        Code = CatalogRules.Code(code).ToUpperInvariant();
        Edit(label, true, order);
    }
    public void Edit(string label, bool active, int order)
    {
        if (order < 0)
            throw new DomainException("Negative sort order.");
        Label = CatalogRules.Required(label, 256, "Label");
        IsActive = active;
        SortOrder = order;
    }
}
public sealed class ProductFieldValue
{
    public Guid ProductId
    {
        get; private set;
    }
    public Guid ProductTypeId
    {
        get; private set;
    }
    public Guid FieldDefinitionId
    {
        get; private set;
    }
    public ProductFieldType FieldType
    {
        get; private set;
    }
    public string? TextValue
    {
        get; private set;
    }
    public decimal? NumberValue
    {
        get; private set;
    }
    public bool? BooleanValue
    {
        get; private set;
    }
    public DateOnly? DateValue
    {
        get; private set;
    }
    public Guid? ChoiceId
    {
        get; private set;
    }
    private ProductFieldValue()
    {
    }
    public ProductFieldValue(Guid productId, Guid typeId, Guid fieldId, ProductFieldType type, string? text = null,
        decimal? number = null, bool? boolean = null, DateOnly? date = null, Guid? choice = null)
    {
        ProductId = productId;
        ProductTypeId = typeId;
        FieldDefinitionId = fieldId;
        FieldType = type;
        TextValue = text;
        NumberValue = number;
        BooleanValue = boolean;
        DateValue = date;
        ChoiceId = choice;
        ValidateShape();
    }
    public void ValidateShape()
    {
        int count = (TextValue is null ? 0 : 1) + (NumberValue.HasValue ? 1 : 0) + (BooleanValue.HasValue ? 1 : 0) + (DateValue.HasValue ? 1 : 0) + (ChoiceId.HasValue ? 1 : 0);
        bool valid = FieldType switch
        {
            ProductFieldType.Text => !string.IsNullOrWhiteSpace(TextValue) && TextValue.Length <= 500,
            ProductFieldType.Number => NumberValue.HasValue && CatalogRules.IsFieldNumber(NumberValue.Value),
            ProductFieldType.Boolean => BooleanValue.HasValue,
            ProductFieldType.Date => DateValue.HasValue,
            ProductFieldType.Choice => ChoiceId.HasValue && ChoiceId != Guid.Empty,
            _ => false
        };
        if (count != 1 || !valid)
            throw new DomainException("Exactly one correctly typed field value is required.");
    }
    public bool SameValue(ProductFieldValue other) => TextValue == other.TextValue && NumberValue == other.NumberValue && BooleanValue == other.BooleanValue && DateValue == other.DateValue && ChoiceId == other.ChoiceId;
    internal void CopyValue(ProductFieldValue other)
    {
        TextValue = other.TextValue;
        NumberValue = other.NumberValue;
        BooleanValue = other.BooleanValue;
        DateValue = other.DateValue;
        ChoiceId = other.ChoiceId;
    }
}
