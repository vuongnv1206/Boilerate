using Boilerate.Application.Catalog.Products;
using Boilerate.Infrastructure.Auth.Permissions;
using Boilerate.Shared.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Boilerate.Host.Controllers.Catalog;

[Route("api/products")]
public sealed class ProductsController : BaseApiController
{
    [HttpPost("search"), MustHavePermission(AppAction.View, AppFunction.Product)]
    public Task<Page<ProductSummary>> Search(ProductSearch request, CancellationToken ct) =>
        Mediator.Send(new SearchProductsRequest(request), ct);

    [HttpGet("{id:guid}"), MustHavePermission(AppAction.View, AppFunction.Product)]
    public Task<ProductDto> Get(Guid id, CancellationToken ct) =>
        Mediator.Send(new GetProductRequest(id), ct);

    [HttpPost, MustHavePermission(AppAction.Create, AppFunction.Product)]
    public Task<ProductDto> Create(CreateProductRequest request, CancellationToken ct) =>
        Mediator.Send(request, ct);

    [HttpPut("{id:guid}"), MustHavePermission(AppAction.Update, AppFunction.Product)]
    public async Task<ActionResult<ProductDto>> Edit(Guid id, UpdateProductContentRequest request, CancellationToken ct) =>
        id != request.Id ? BadRequest() : Ok(await Mediator.Send(request, ct));

    [HttpPut("{id:guid}/editor"), MustHavePermission(AppAction.Update, AppFunction.Product)]
    public async Task<ActionResult<ProductDto>> SaveEditor(Guid id, SaveProductEditorRequest request, CancellationToken ct) =>
        id != request.Id ? BadRequest() : Ok(await Mediator.Send(request, ct));

    [HttpDelete("{id:guid}"), MustHavePermission(AppAction.Delete, AppFunction.Product)]
    public Task<bool> Delete(Guid id, [FromQuery] string version, CancellationToken ct) =>
        Mediator.Send(new DeleteProductRequest(id, version), ct);

    [HttpPost("bulk-status"), MustHavePermission(AppAction.Publish, AppFunction.Product)]
    public Task<BulkProductStatusResult> BulkStatus(BulkUpdateProductStatusRequest request, CancellationToken ct) =>
        Mediator.Send(request, ct);

    [HttpPost("{id:guid}/status"), MustHavePermission(AppAction.Publish, AppFunction.Product)]
    public async Task<ActionResult<ProductDto>> Status(Guid id, ChangeProductStatusRequest request, CancellationToken ct) =>
        id != request.Id ? BadRequest() : Ok(await Mediator.Send(request, ct));

    [HttpPost("variants/preview"), MustHavePermission(AppAction.View, AppFunction.Product)]
    public Task<List<Dictionary<string, string>>> Preview(PreviewVariantsRequest request, CancellationToken ct) =>
        Mediator.Send(request, ct);

    [HttpPost("{id:guid}/variants"), MustHavePermission(AppAction.Update, AppFunction.Product)]
    public async Task<ActionResult<ProductDto>> AddVariant(Guid id, AddProductVariantRequest request, CancellationToken ct) =>
        id != request.ProductId ? BadRequest() : Ok(await Mediator.Send(request, ct));

    [HttpPut("{id:guid}/variants/{variantId:guid}"), MustHavePermission(AppAction.Update, AppFunction.Product)]
    public async Task<ActionResult<ProductDto>> EditVariant(
        Guid id,
        Guid variantId,
        UpdateProductVariantRequest request,
        CancellationToken ct) =>
        id != request.ProductId || variantId != request.VariantId ? BadRequest() : Ok(await Mediator.Send(request, ct));

    [HttpPut("{id:guid}/default-variant"), MustHavePermission(AppAction.Update, AppFunction.Product)]
    public async Task<ActionResult<ProductDto>> Default(Guid id, SetDefaultVariantRequest request, CancellationToken ct) =>
        id != request.ProductId ? BadRequest() : Ok(await Mediator.Send(request, ct));

    [HttpPut("{id:guid}/options/{optionId:guid}"), MustHavePermission(AppAction.Update, AppFunction.Product)]
    public async Task<ActionResult<ProductDto>> Option(
        Guid id,
        Guid optionId,
        UpdateProductOptionRequest request,
        CancellationToken ct) =>
        id != request.ProductId || optionId != request.OptionId ? BadRequest() : Ok(await Mediator.Send(request, ct));
}
