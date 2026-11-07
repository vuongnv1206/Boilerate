using Boilerate.Application.Inventory.Physical;
using Boilerate.Infrastructure.Auth.Permissions;
using Boilerate.Shared.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Boilerate.Host.Controllers.Inventory;

[Route("api/inventory")]
public sealed class InventoryController : BaseApiController
{
    [HttpPost("initialize"), MustHavePermission(AppAction.Initialize, AppFunction.Inventory)]
    public Task<Guid> Initialize(CancellationToken ct) =>
        Mediator.Send(new InitializeInventoryRequest(), ct);

    [HttpGet, MustHavePermission(AppAction.View, AppFunction.Inventory)]
    public Task<Page<InventoryDto>> List(
        CancellationToken ct,
        int page = 1,
        int size = 20,
        Guid? productId = null,
        Guid? variantId = null) =>
        Mediator.Send(new GetInventoryItemsRequest(page, size, productId, variantId), ct);

    [HttpPost("{id:guid}/adjustments"), MustHavePermission(AppAction.Adjust, AppFunction.Inventory)]
    public async Task<ActionResult<MovementDto>> Adjust(Guid id, AdjustInventoryRequest request, CancellationToken ct) =>
        id != request.InventoryItemId ? BadRequest() : Ok(await Mediator.Send(request, ct));

    [HttpGet("{id:guid}/movements"), MustHavePermission(AppAction.View, AppFunction.Inventory)]
    public Task<Page<MovementDto>> History(Guid id, CancellationToken ct, int page = 1, int size = 20) =>
        Mediator.Send(new GetStockMovementsRequest(id, page, size), ct);
}

