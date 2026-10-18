using Boilerate.Domain.Catalog;
using MediatR;
using Microsoft.AspNetCore.Http;
namespace Boilerate.Application.Catalog;

public sealed record ChoiceInput(string Code, string Label);
public sealed record OptionInput(string Code, string Name, List<ChoiceInput> Values);
public sealed record VariantInput(string Sku, decimal? Price, decimal? CompareAtPrice, Dictionary<string, string> Options);
public sealed record FieldInput(Guid FieldDefinitionId, ProductFieldType FieldType, string? TextValue = null, decimal? NumberValue = null,
    bool? BooleanValue = null, DateOnly? DateValue = null, Guid? ChoiceId = null)
{
    public ProductFieldValue ToValue(Guid productId, Guid typeId) => new(productId, typeId, FieldDefinitionId, FieldType, TextValue, NumberValue, BooleanValue, DateValue, ChoiceId);
}
public sealed record MediaInput(Guid MediaAssetId, int SortOrder, string? AltText);
public sealed record ProductContent(string Name, string Slug, string? Description, List<Guid> CategoryIds, List<FieldInput> Fields, List<MediaInput> Media);
public sealed record CreateProduct(Guid ProductTypeId, string ProductTypeVersion, ProductContent Content, List<OptionInput> Options, List<VariantInput> Variants,
    ProductFulfillmentKind FulfillmentKind = ProductFulfillmentKind.Physical) : IRequest<ProductDto>;
public sealed record EditProduct(Guid Id, string Version, string ProductTypeVersion, ProductContent Content) : IRequest<ProductDto>;
public sealed record ChangeProductStatus(Guid Id, string Version, ProductStatus Status) : IRequest<ProductDto>;
public sealed record AddVariant(Guid ProductId, string Version, VariantInput Variant) : IRequest<ProductDto>;
public sealed record EditVariant(Guid ProductId, Guid VariantId, string Version, decimal? Price, decimal? CompareAtPrice, bool IsActive, Guid? ImageMediaId) : IRequest<ProductDto>;
public sealed record SetDefaultVariant(Guid ProductId, Guid VariantId, string Version) : IRequest<ProductDto>;
public sealed record EditOption(Guid ProductId, Guid OptionId, string Version, string Name, int SortOrder, List<ChoiceInput> AddValues, List<OptionValueEdit> Values) : IRequest<ProductDto>;
public sealed record OptionValueEdit(Guid Id, string Label, int SortOrder);
public sealed record PreviewVariants(List<OptionInput> Options) : IRequest<List<Dictionary<string, string>>>;
public sealed record SaveCategory(Guid? Id, string? Version, string Name, string Slug, string? Description, Guid? ParentId, int SortOrder, bool IsActive) : IRequest<Category>;
public sealed record DeleteCategory(Guid Id, string Version) : IRequest<bool>;
public sealed record CreateProductType(string Code, string Name, List<FieldDefinitionInput> Fields) : IRequest<ProductType>;
public sealed record FieldDefinitionInput(string Code, ProductFieldType DataType, string Label, string? HelpText, bool IsRequired, bool IsFilterable, List<ChoiceInput> Choices);
public sealed record EditProductType(Guid Id, string Version, string Name, bool IsActive, List<FieldDefinitionInput> AddFields, List<FieldDefinitionEdit> Fields) : IRequest<ProductType>;
public sealed record FieldDefinitionEdit(Guid Id, string Label, string? HelpText, bool IsFilterable, bool IsActive, int SortOrder, List<ChoiceInput> AddChoices, List<FieldChoiceEdit> Choices);
public sealed record FieldChoiceEdit(Guid Id, string Label, bool IsActive, int SortOrder);
public sealed record AdjustInventory(Guid InventoryItemId, string Version, Guid OperationId, int Delta, string Reason) : IRequest<MovementDto>;
public sealed record InitializeCommerce : IRequest<Guid>;
public sealed record ReplayOutbox(Guid MessageId) : IRequest<bool>;
public sealed record VariantDto(Guid Id, string Sku, string CombinationKey, decimal? Price, decimal? CompareAtPrice, string Currency, bool IsDefault, bool IsActive, Guid? ImageMediaId, bool InStock, IReadOnlyCollection<VariantSelectionDto> Selections);
public sealed record ProductDto(Guid Id, Guid ProductTypeId, string Name, string Slug, string? Description, ProductStatus Status, string Version,
    IReadOnlyCollection<Guid> CategoryIds, IReadOnlyCollection<OptionDto> Options, IReadOnlyCollection<VariantDto> Variants,
    IReadOnlyCollection<FieldValueDto> Fields, IReadOnlyCollection<MediaInput> Media, ProductFulfillmentKind FulfillmentKind = ProductFulfillmentKind.Physical);
public sealed record VariantSelectionDto(Guid OptionId, Guid OptionValueId);
public sealed record OptionDto(Guid Id, string Code, string Name, int SortOrder, IReadOnlyCollection<OptionValueDto> Values);
public sealed record OptionValueDto(Guid Id, string Code, string Label, int SortOrder);
public sealed record FieldValueDto(Guid FieldDefinitionId, ProductFieldType FieldType, string Label, string? TextValue, decimal? NumberValue, bool? BooleanValue, DateOnly? DateValue, Guid? ChoiceId, string? ChoiceLabel);
public sealed record ProductSummary(Guid Id, string Name, string Slug, ProductStatus Status, decimal? FromPrice, bool InStock,
    ProductFulfillmentKind FulfillmentKind = ProductFulfillmentKind.Physical);
public sealed record Page<T>(List<T> Items, int Total, int PageNumber, int PageSize);
public sealed record InventoryDto(Guid Id, Guid VariantId, Guid LocationId, int OnHand, string Version, int Reserved = 0)
{
    public int Available => OnHand - Reserved;
}
public sealed record MovementDto(Guid Id, Guid InventoryItemId, Guid OperationId, int Delta, int QuantityAfter, string Reason, Guid ActorId, DateTime CreatedAt);
public sealed record MediaDto(Guid Id, string ContentType, long Size, string State, string Version);
public sealed record FieldFilter(Guid FieldId, string Operator, string? TextValue = null, decimal? NumberValue = null, decimal? NumberTo = null,
    bool? BooleanValue = null, DateOnly? DateValue = null, DateOnly? DateTo = null, List<Guid>? ChoiceIds = null);
public sealed class ProductSearch
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search
    {
        get; set;
    }
    public Guid? CategoryId
    {
        get; set;
    }
    public Guid? ProductTypeId
    {
        get; set;
    }
    public ProductStatus? Status
    {
        get; set;
    }
    public decimal? MinimumPrice
    {
        get; set;
    }
    public decimal? MaximumPrice
    {
        get; set;
    }
    public bool? InStock
    {
        get; set;
    }
    public List<FieldFilter> Fields { get; set; } = [];
    public List<Guid> OptionValueIds { get; set; } = [];
}
public interface ICommerceService
{
    Task<ProductDto> CreateAsync(CreateProduct request, CancellationToken ct);
    Task<ProductDto> EditAsync(EditProduct request, CancellationToken ct);
    Task<ProductDto> StatusAsync(ChangeProductStatus request, CancellationToken ct);
    Task<ProductDto> AddVariantAsync(AddVariant request, CancellationToken ct);
    Task<ProductDto> EditVariantAsync(EditVariant request, CancellationToken ct);
    Task<ProductDto> DefaultAsync(SetDefaultVariant request, CancellationToken ct);
    Task<ProductDto> OptionAsync(EditOption request, CancellationToken ct);
    List<Dictionary<string, string>> Preview(PreviewVariants request);
    Task<Category> CategoryAsync(SaveCategory request, CancellationToken ct);
    Task<bool> DeleteCategoryAsync(DeleteCategory request, CancellationToken ct);
    Task<ProductType> CreateTypeAsync(CreateProductType request, CancellationToken ct);
    Task<ProductType> EditTypeAsync(EditProductType request, CancellationToken ct);
    Task<MovementDto> AdjustAsync(AdjustInventory request, CancellationToken ct);
    Task<Guid> InitializeAsync(CancellationToken ct);
    Task<bool> ReplayAsync(ReplayOutbox request, CancellationToken ct);
    Task<ProductDto> GetAsync(Guid id, bool publishedOnly, CancellationToken ct);
    Task<ProductDto> GetBySlugAsync(string slug, CancellationToken ct);
    Task<Page<ProductSummary>> SearchAsync(ProductSearch request, bool publishedOnly, CancellationToken ct);
    Task<List<Category>> CategoriesAsync(bool publicOnly, CancellationToken ct);
    Task<List<ProductType>> TypesAsync(CancellationToken ct);
    Task<ProductType> TypeAsync(Guid id, CancellationToken ct);
    Task<Page<InventoryDto>> InventoryAsync(int page, int size, CancellationToken ct);
    Task<Page<MovementDto>> MovementsAsync(Guid itemId, int page, int size, CancellationToken ct);
    Task<List<DigitalStockDto>> ImportDigitalAsync(ImportDigitalStock request, CancellationToken ct);
    Task<DigitalStockDto> SaveSharedAsync(SaveSharedDigitalContent request, CancellationToken ct);
    Task<DigitalStockDto> SetDigitalStockAvailabilityAsync(SetDigitalStockAvailability request, CancellationToken ct);
    Task<Page<DigitalStockDto>> DigitalStockAsync(Guid variantId, int page, int size, CancellationToken ct);
    Task<DigitalSecretDto> RevealDigitalStockAsync(Guid id, CancellationToken ct);
}
public interface ICommerceMediaService
{
    Task<MediaDto> UploadAsync(IFormFile file, CancellationToken ct);
    Task<(Stream Stream, string ContentType)> OpenAsync(Guid id, bool publishedOnly, CancellationToken ct);
}
