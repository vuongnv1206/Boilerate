using MediatR;
using Boilerate.Domain.Catalog;
namespace Boilerate.Application.Catalog;

public sealed class CreateProductHandler(ICommerceService service) : IRequestHandler<CreateProduct, ProductDto>
{
    public Task<ProductDto> Handle(CreateProduct request, CancellationToken ct) => service.CreateAsync(request, ct);
}
public sealed class EditProductHandler(ICommerceService service) : IRequestHandler<EditProduct, ProductDto>
{
    public Task<ProductDto> Handle(EditProduct request, CancellationToken ct) => service.EditAsync(request, ct);
}
public sealed class SaveProductEditorHandler(ICommerceService service) : IRequestHandler<SaveProductEditor, ProductDto>
{
    public Task<ProductDto> Handle(SaveProductEditor request, CancellationToken ct) => service.SaveEditorAsync(request, ct);
}
public sealed class BulkProductStatusHandler(ICommerceService service) : IRequestHandler<BulkProductStatus, BulkProductStatusResult>
{
    public Task<BulkProductStatusResult> Handle(BulkProductStatus request, CancellationToken ct) => service.BulkStatusAsync(request, ct);
}
public sealed class ChangeProductStatusHandler(ICommerceService service) : IRequestHandler<ChangeProductStatus, ProductDto>
{
    public Task<ProductDto> Handle(ChangeProductStatus request, CancellationToken ct) => service.StatusAsync(request, ct);
}
public sealed class AddVariantHandler(ICommerceService service) : IRequestHandler<AddVariant, ProductDto>
{
    public Task<ProductDto> Handle(AddVariant request, CancellationToken ct) => service.AddVariantAsync(request, ct);
}
public sealed class EditVariantHandler(ICommerceService service) : IRequestHandler<EditVariant, ProductDto>
{
    public Task<ProductDto> Handle(EditVariant request, CancellationToken ct) => service.EditVariantAsync(request, ct);
}
public sealed class SetDefaultVariantHandler(ICommerceService service) : IRequestHandler<SetDefaultVariant, ProductDto>
{
    public Task<ProductDto> Handle(SetDefaultVariant request, CancellationToken ct) => service.DefaultAsync(request, ct);
}
public sealed class EditOptionHandler(ICommerceService service) : IRequestHandler<EditOption, ProductDto>
{
    public Task<ProductDto> Handle(EditOption request, CancellationToken ct) => service.OptionAsync(request, ct);
}
public sealed class SaveCategoryHandler(ICommerceService service) : IRequestHandler<SaveCategory, Category>
{
    public Task<Category> Handle(SaveCategory request, CancellationToken ct) => service.CategoryAsync(request, ct);
}
public sealed class DeleteCategoryHandler(ICommerceService service) : IRequestHandler<DeleteCategory, bool>
{
    public Task<bool> Handle(DeleteCategory request, CancellationToken ct) => service.DeleteCategoryAsync(request, ct);
}
public sealed class CreateProductTypeHandler(ICommerceService service) : IRequestHandler<CreateProductType, ProductType>
{
    public Task<ProductType> Handle(CreateProductType request, CancellationToken ct) => service.CreateTypeAsync(request, ct);
}
public sealed class EditProductTypeHandler(ICommerceService service) : IRequestHandler<EditProductType, ProductType>
{
    public Task<ProductType> Handle(EditProductType request, CancellationToken ct) => service.EditTypeAsync(request, ct);
}
public sealed class AdjustInventoryHandler(ICommerceService service) : IRequestHandler<AdjustInventory, MovementDto>
{
    public Task<MovementDto> Handle(AdjustInventory request, CancellationToken ct) => service.AdjustAsync(request, ct);
}
public sealed class ReplayOutboxHandler(ICommerceService service) : IRequestHandler<ReplayOutbox, bool>
{
    public Task<bool> Handle(ReplayOutbox request, CancellationToken ct) => service.ReplayAsync(request, ct);
}
public sealed class InitializeCommerceHandler(ICommerceService service) : IRequestHandler<InitializeCommerce, Guid>
{
    public Task<Guid> Handle(InitializeCommerce request, CancellationToken ct) => service.InitializeAsync(ct);
}
public sealed class PreviewVariantsHandler(ICommerceService service) : IRequestHandler<PreviewVariants, List<Dictionary<string, string>>>
{
    public Task<List<Dictionary<string, string>>> Handle(PreviewVariants request, CancellationToken ct) => Task.FromResult(service.Preview(request));
}
