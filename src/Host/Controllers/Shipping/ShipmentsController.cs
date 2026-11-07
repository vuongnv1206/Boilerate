using Boilerate.Application.Shipping;
using Boilerate.Infrastructure.Auth.Permissions;
using Boilerate.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Boilerate.Host.Controllers.Shipping;

[Route("api/admin/orders/{orderId:guid}/shipments"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminShipmentsController : BaseApiController
{
    [HttpGet, MustHavePermission(AppAction.View, AppFunction.Shipping)]
    public Task<List<ShipmentDto>> List(Guid orderId, CancellationToken ct) =>
        Mediator.Send(new GetAdminOrderShipmentsRequest(orderId), ct);

    [HttpPost, MustHavePermission(AppAction.Dispatch, AppFunction.Shipping)]
    public Task<ShipmentDto> Dispatch(Guid orderId, DispatchShipment request, CancellationToken ct) =>
        Mediator.Send(new DispatchShipmentRequest(orderId, request), ct);

    [HttpPost("{shipmentId:guid}/deliver"), MustHavePermission(AppAction.Deliver, AppFunction.Shipping)]
    public Task<ShipmentDto> Deliver(Guid orderId, Guid shipmentId, DeliverShipment request, CancellationToken ct) =>
        Mediator.Send(new DeliverShipmentRequest(orderId, shipmentId, request), ct);
}

[Authorize, Route("api/orders/{orderId:guid}/shipments"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class MyShipmentsController : BaseApiController
{
    [HttpGet]
    public Task<List<ShipmentDto>> List(Guid orderId, CancellationToken ct) =>
        Mediator.Send(new GetMyOrderShipmentsRequest(orderId), ct);
}

