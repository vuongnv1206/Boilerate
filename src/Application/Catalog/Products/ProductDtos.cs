using Boilerate.Application.Catalog.ProductTypes;
using Boilerate.Application.Common.Validation;
using Boilerate.Domain.Catalog;
using FluentValidation;

namespace Boilerate.Application.Catalog.Products;

public sealed record OptionInput(string Code, string Name, List<ChoiceInput> Values);

public sealed record VariantInput(
    string Sku,
    decimal? Price,
    decimal? CompareAtPrice,
    Dictionary<string, string> Options,
    bool IsActive = true,
    Guid? ImageMediaId = null);

public sealed record FieldInput(
    Guid FieldDefinitionId,
    ProductFieldType FieldType,
    string? TextValue = null,
    decimal? NumberValue = null,
    bool? BooleanValue = null,
    DateOnly? DateValue = null,
    Guid? ChoiceId = null)
{
    public ProductFieldValue ToValue(Guid productId, Guid typeId) =>
        new(productId, typeId, FieldDefinitionId, FieldType, TextValue, NumberValue, BooleanValue, DateValue, ChoiceId);
}

public sealed record MediaInput(Guid MediaAssetId, int SortOrder, string? AltText);

public sealed record ProductContent(
    string Name,
    string Slug,
    string? Description,
    List<Guid> CategoryIds,
    List<FieldInput> Fields,
    List<MediaInput> Media);

public sealed record OptionValueEdit(Guid Id, string Label, int SortOrder);

public sealed record OptionEditorInput(
    Guid OptionId,
    string Name,
    int SortOrder,
    List<ChoiceInput> AddValues,
    List<OptionValueEdit> Values);

public sealed record VariantEditorInput(
    Guid VariantId,
    decimal? Price,
    decimal? CompareAtPrice,
    bool IsActive,
    Guid? ImageMediaId);

public sealed record ProductVersionInput(Guid Id, string Version);

public sealed record ProductStatusResult(Guid Id, ProductStatus Status, string Version);

public sealed record BulkProductStatusResult(List<ProductStatusResult> Items);

public sealed record VariantDto(
    Guid Id,
    string Sku,
    string CombinationKey,
    decimal? Price,
    decimal? CompareAtPrice,
    string Currency,
    bool IsDefault,
    bool IsActive,
    Guid? ImageMediaId,
    bool InStock,
    IReadOnlyCollection<VariantSelectionDto> Selections,
    bool TrackInventory = true,
    decimal? Weight = null,
    decimal? Width = null,
    decimal? Height = null,
    decimal? Length = null,
    IReadOnlyCollection<MediaInput>? Images = null,
    int? Quantity = null,
    string? InventoryVersion = null);

public sealed record ProductDto(
    Guid Id,
    Guid ProductTypeId,
    string Name,
    string Slug,
    string? Description,
    ProductStatus Status,
    string Version,
    IReadOnlyCollection<Guid> CategoryIds,
    IReadOnlyCollection<OptionDto> Options,
    IReadOnlyCollection<VariantDto> Variants,
    IReadOnlyCollection<FieldValueDto> Fields,
    IReadOnlyCollection<MediaInput> Media,
    ProductFulfillmentKind FulfillmentKind = ProductFulfillmentKind.Physical,
    DateTime CreatedOn = default,
    DateTime ModifiedOn = default);

public sealed record VariantSelectionDto(Guid OptionId, Guid OptionValueId);

public sealed record OptionDto(
    Guid Id,
    string Code,
    string Name,
    int SortOrder,
    IReadOnlyCollection<OptionValueDto> Values);

public sealed record OptionValueDto(Guid Id, string Code, string Label, int SortOrder);

public sealed record FieldValueDto(
    Guid FieldDefinitionId,
    ProductFieldType FieldType,
    string Label,
    string? TextValue,
    decimal? NumberValue,
    bool? BooleanValue,
    DateOnly? DateValue,
    Guid? ChoiceId,
    string? ChoiceLabel);

public sealed record ProductSummary(
    Guid Id,
    string Name,
    string Slug,
    ProductStatus Status,
    decimal? FromPrice,
    bool InStock,
    ProductFulfillmentKind FulfillmentKind = ProductFulfillmentKind.Physical,
    Guid ProductTypeId = default,
    string ProductTypeName = "",
    Guid? ThumbnailMediaId = null,
    string Version = "",
    DateTime ModifiedOn = default);

public sealed record FieldFilter(
    Guid FieldId,
    string Operator,
    string? TextValue = null,
    decimal? NumberValue = null,
    decimal? NumberTo = null,
    bool? BooleanValue = null,
    DateOnly? DateValue = null,
    DateOnly? DateTo = null,
    List<Guid>? ChoiceIds = null);

public sealed class ProductSearch
{
    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    public string? Search { get; set; }

    public Guid? CategoryId { get; set; }

    public Guid? ProductTypeId { get; set; }

    public ProductStatus? Status { get; set; }

    public ProductFulfillmentKind? FulfillmentKind { get; set; }

    public string SortBy { get; set; } = "modified";

    public string SortDirection { get; set; } = "desc";

    public decimal? MinimumPrice { get; set; }

    public decimal? MaximumPrice { get; set; }

    public bool? InStock { get; set; }

    public List<FieldFilter> Fields { get; set; } = [];

    public List<Guid> OptionValueIds { get; set; } = [];
}

// Stable IDs identify draft rows; labels and optional SKU never serve as identity.
public sealed record CatalogChoiceState(Guid Id, string Label);

public sealed record CatalogOptionState(Guid Id, string Name, List<CatalogChoiceState> Values);

public sealed record CatalogVariantState(
    Guid Id,
    string? Sku,
    decimal? Price,
    decimal? CompareAtPrice,
    Dictionary<Guid, Guid> Selections,
    bool IsActive = true,
    Guid? ImageMediaId = null,
    bool TrackInventory = true,
    List<MediaInput>? Images = null,
    decimal? Weight = null,
    decimal? Width = null,
    decimal? Height = null,
    decimal? Length = null,
    int? Quantity = null,
    string? InventoryVersion = null,
    Guid? InventoryOperationId = null);

public sealed record CatalogConfiguration(
    List<CatalogOptionState> Options,
    List<CatalogVariantState> Variants,
    Guid DefaultVariantId);

public sealed class ProductContentValidator : CustomValidator<ProductContent>
{
    public ProductContentValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(20000);
        RuleFor(x => x.CategoryIds).NotNull().Must(x => x is null || x.Count <= 100);
        RuleFor(x => x.Fields).NotNull().Must(x => x is null || x.Count <= 100);
        RuleFor(x => x.Media).NotNull().Must(x => x is null || x.Count <= 31000);
        RuleForEach(x => x.Fields).NotNull();
        RuleForEach(x => x.Media).NotNull();
    }
}

public sealed class VariantInputValidator : CustomValidator<VariantInput>
{
    public VariantInputValidator()
    {
        RuleFor(x => x.Sku).MaximumLength(100);
        RuleFor(x => x.Options).NotNull().Must(x => x is null || x.Count <= 10);
    }
}

public sealed class OptionInputValidator : CustomValidator<OptionInput>
{
    public OptionInputValidator()
    {
        RuleFor(x => x.Code).MaximumLength(100);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Values).NotEmpty().Must(x => x is null || x.Count <= 200);
        RuleForEach(x => x.Values).NotNull();
    }
}

