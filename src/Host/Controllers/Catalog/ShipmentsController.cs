using Boilerate.Application.Ordering;
using Boilerate.Infrastructure.Auth.Permissions;
using Boilerate.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Boilerate.Host.Controllers.Catalog;

[Route("api/admin/orders/{orderId:guid}/shipments"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminShipmentsController(IShippingService shipping) : BaseApiController
{
    [HttpGet, MustHavePermission(AppAction.View, AppFunction.Shipping)]
    public Task<List<ShipmentDto>> List(Guid orderId, CancellationToken ct) => shipping.ShipmentsAsync(orderId, true, ct);

    [HttpPost, MustHavePermission(AppAction.Dispatch, AppFunction.Shipping)]
    public Task<ShipmentDto> Dispatch(Guid orderId, DispatchShipment request, CancellationToken ct) => shipping.DispatchShipmentAsync(orderId, request, ct);

    [HttpPost("{shipmentId:guid}/deliver"), MustHavePermission(AppAction.Deliver, AppFunction.Shipping)]
    public Task<ShipmentDto> Deliver(Guid orderId, Guid shipmentId, DeliverShipment request, CancellationToken ct) =>
        shipping.DeliverShipmentAsync(orderId, shipmentId, request, ct);
}

[Authorize, Route("api/orders/{orderId:guid}/shipments"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class MyShipmentsController(IShippingService shipping) : BaseApiController
{
    [HttpGet]
    public Task<List<ShipmentDto>> List(Guid orderId, CancellationToken ct) => shipping.ShipmentsAsync(orderId, false, ct);
}
