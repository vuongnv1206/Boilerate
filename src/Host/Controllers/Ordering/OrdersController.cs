using Boilerate.Application.Ordering.Cart;
using Boilerate.Application.Ordering.Orders;
using Boilerate.Application.Ordering.Shared;
using Boilerate.Infrastructure.Auth.Permissions;
using Boilerate.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Boilerate.Host.Controllers.Ordering;

[Authorize, Route("api/cart"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CartController : BaseApiController
{
    [HttpGet]
    public Task<CartDto> Get(CancellationToken ct) =>
        Mediator.Send(new GetMyCartRequest(), ct);

    [HttpPut]
    public Task<CartDto> Save(SaveCart request, CancellationToken ct) =>
        Mediator.Send(new SaveMyCartRequest(request), ct);

    [HttpPut("discount")]
    public Task<CartDto> Discount(SetCartDiscount request, CancellationToken ct) =>
        Mediator.Send(new SetMyCartDiscountRequest(request), ct);
}

[Authorize, Route("api/orders"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class OrdersController : BaseApiController
{
    [HttpPost("checkout")]
    public Task<OrderDto> Checkout(Checkout request, CancellationToken ct) =>
        Mediator.Send(new CheckoutMyCartRequest(request), ct);

    [HttpGet("mine")]
    public Task<Page<OrderSummary>> Mine(CancellationToken ct, int page = 1, int size = 20) =>
        Mediator.Send(new GetMyOrdersRequest(page, size), ct);

    [HttpGet("{id:guid}")]
    public Task<OrderDto> Get(Guid id, CancellationToken ct) =>
        Mediator.Send(new GetMyOrderDetailRequest(id), ct);

    [HttpPost("{id:guid}/cancel")]
    public Task<OrderDto> Cancel(Guid id, CancelOrder request, CancellationToken ct) =>
        Mediator.Send(new CancelMyOrderRequest(id, request), ct);
}

[Route("api/admin/orders"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminOrdersController : BaseApiController
{
    [HttpGet, MustHavePermission(AppAction.View, AppFunction.Order)]
    public Task<Page<OrderSummary>> List([FromQuery] AdminOrderQuery query, CancellationToken ct) =>
        Mediator.Send(new GetAdminOrdersRequest(query), ct);

    [HttpGet("{id:guid}"), MustHavePermission(AppAction.View, AppFunction.Order)]
    public Task<OrderDto> Get(Guid id, CancellationToken ct) =>
        Mediator.Send(new GetAdminOrderDetailRequest(id), ct);

    [HttpGet("{id:guid}/history"), MustHavePermission(AppAction.View, AppFunction.Order)]
    public Task<IReadOnlyList<OrderHistoryEntryDto>> History(Guid id, CancellationToken ct) =>
        Mediator.Send(new GetAdminOrderHistoryRequest(id), ct);

    [HttpPost("{id:guid}/cancel"), MustHavePermission(AppAction.Cancel, AppFunction.Order)]
    public Task<OrderDto> Cancel(Guid id, CancelOrder request, CancellationToken ct) =>
        Mediator.Send(new CancelAdminOrderRequest(id, request), ct);
}

