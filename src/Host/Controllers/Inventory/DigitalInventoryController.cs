using Boilerate.Application.Inventory.Digital;
using Boilerate.Infrastructure.Auth.Permissions;
using Boilerate.Shared.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Boilerate.Host.Controllers.Inventory;

[Route("api/digital-inventory"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DigitalInventoryController : BaseApiController
{
    [HttpGet("variants/{variantId:guid}"), MustHavePermission(AppAction.View, AppFunction.DigitalInventory)]
    public Task<Page<DigitalStockDto>> List(Guid variantId, CancellationToken ct, int page = 1, int size = 20) =>
        Mediator.Send(new GetDigitalStockRequest(variantId, page, size), ct);

    [HttpPost("variants/{variantId:guid}/items"), MustHavePermission(AppAction.Import, AppFunction.DigitalInventory)]
    public async Task<ActionResult<List<DigitalStockDto>>> Import(
        Guid variantId,
        ImportDigitalStockRequest request,
        CancellationToken ct) =>
        variantId != request.VariantId ? BadRequest() : Ok(await Mediator.Send(request, ct));

    [HttpPut("variants/{variantId:guid}/shared-content"), MustHavePermission(AppAction.Update, AppFunction.DigitalInventory)]
    public async Task<ActionResult<DigitalStockDto>> Shared(
        Guid variantId,
        SaveSharedDigitalContentRequest request,
        CancellationToken ct) =>
        variantId != request.VariantId ? BadRequest() : Ok(await Mediator.Send(request, ct));

    [HttpPut("variants/{variantId:guid}/items/{id:guid}/availability"), MustHavePermission(AppAction.Update, AppFunction.DigitalInventory)]
    public async Task<ActionResult<DigitalStockDto>> Availability(
        Guid variantId,
        Guid id,
        SetDigitalStockAvailabilityRequest request,
        CancellationToken ct) =>
        variantId != request.VariantId || id != request.StockItemId ? BadRequest() : Ok(await Mediator.Send(request, ct));

    [HttpPost("items/{id:guid}/reveal"), MustHavePermission(AppAction.Reveal, AppFunction.DigitalInventory)]
    public Task<DigitalSecretDto> Reveal(Guid id, CancellationToken ct) =>
        Mediator.Send(new RevealDigitalStockRequest(id), ct);
}

