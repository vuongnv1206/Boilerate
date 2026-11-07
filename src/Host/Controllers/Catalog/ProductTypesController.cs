using Boilerate.Application.Catalog.ProductTypes;
using Boilerate.Domain.Catalog;
using Boilerate.Infrastructure.Auth.Permissions;
using Boilerate.Shared.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Boilerate.Host.Controllers.Catalog;

[Route("api/product-types")]
public sealed class ProductTypesController : BaseApiController
{
    [HttpGet, MustHavePermission(AppAction.View, AppFunction.ProductType)]
    public Task<List<ProductType>> List(CancellationToken ct) =>
        Mediator.Send(new GetProductTypesRequest(), ct);

    [HttpGet("{id:guid}"), MustHavePermission(AppAction.View, AppFunction.ProductType)]
    public Task<ProductType> Get(Guid id, CancellationToken ct) =>
        Mediator.Send(new GetProductTypeRequest(id), ct);

    [HttpPost, MustHavePermission(AppAction.Create, AppFunction.ProductType)]
    public Task<ProductType> Create(CreateProductTypeRequest request, CancellationToken ct) =>
        Mediator.Send(request, ct);

    [HttpPut("{id:guid}"), MustHavePermission(AppAction.Update, AppFunction.ProductType)]
    public async Task<ActionResult<ProductType>> Edit(Guid id, UpdateProductTypeRequest request, CancellationToken ct) =>
        id != request.Id ? BadRequest() : Ok(await Mediator.Send(request, ct));
}
