using Boilerate.Application.Catalog;
using Boilerate.Infrastructure.Auth.Permissions;
using Boilerate.Shared.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Boilerate.Host.Controllers.Catalog;

[Route("api/inventory")]
public sealed class InventoryController(ICommerceService commerce) : BaseApiController
{
    [HttpPost("initialize"), MustHavePermission(AppAction.Initialize, AppFunction.Inventory)]
    public Task<Guid> Initialize(CancellationToken ct) => Mediator.Send(new InitializeCommerce(), ct);
    [HttpGet, MustHavePermission(AppAction.View, AppFunction.Inventory)]
    public Task<Page<InventoryDto>> List(CancellationToken ct, int page = 1, int size = 20, Guid? productId = null, Guid? variantId = null)
        => commerce.InventoryAsync(page, size, ct, productId, variantId);
    [HttpPost("{id:guid}/adjustments"), MustHavePermission(AppAction.Adjust, AppFunction.Inventory)]
    public async Task<ActionResult<MovementDto>> Adjust(Guid id, AdjustInventory request, CancellationToken ct)
        => id != request.InventoryItemId ? BadRequest() : Ok(await Mediator.Send(request, ct));
    [HttpGet("{id:guid}/movements"), MustHavePermission(AppAction.View, AppFunction.Inventory)]
    public Task<Page<MovementDto>> History(Guid id, CancellationToken ct, int page = 1, int size = 20) => commerce.MovementsAsync(id, page, size, ct);
}
