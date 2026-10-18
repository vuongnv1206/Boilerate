using Boilerate.Application.Catalog;
using Boilerate.Application.Ordering;
using Boilerate.Infrastructure.Auth.Permissions;
using Boilerate.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Boilerate.Host.Controllers.Catalog;

[Authorize, Route("api/cart"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CartController(IOrderingService ordering) : BaseApiController
{
    [HttpGet]
    public Task<CartDto> Get(CancellationToken ct) => ordering.GetCartAsync(ct);
    [HttpPut]
    public Task<CartDto> Save(SaveCart request, CancellationToken ct) => ordering.SaveCartAsync(request, ct);
}

[Authorize, Route("api/orders"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class OrdersController(IOrderingService ordering) : BaseApiController
{
    [HttpPost("checkout")]
    public Task<OrderDto> Checkout(Checkout request, CancellationToken ct) => ordering.CheckoutAsync(request, ct);
    [HttpGet("mine")]
    public Task<Page<OrderSummary>> Mine(CancellationToken ct, int page = 1, int size = 20) => ordering.OrdersAsync(page, size, false, ct);
    [HttpGet("{id:guid}")]
    public Task<OrderDto> Get(Guid id, CancellationToken ct) => ordering.GetOrderAsync(id, false, ct);
    [HttpPost("{id:guid}/cancel")]
    public Task<OrderDto> Cancel(Guid id, CancelOrder request, CancellationToken ct) => ordering.CancelAsync(id, request, false, ct);
}

[Route("api/admin/orders"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminOrdersController(IOrderingService ordering) : BaseApiController
{
    [HttpGet, MustHavePermission(AppAction.View, AppFunction.Order)]
    public Task<Page<OrderSummary>> List(CancellationToken ct, int page = 1, int size = 20) => ordering.OrdersAsync(page, size, true, ct);
    [HttpGet("{id:guid}"), MustHavePermission(AppAction.View, AppFunction.Order)]
    public Task<OrderDto> Get(Guid id, CancellationToken ct) => ordering.GetOrderAsync(id, true, ct);
    [HttpPost("{id:guid}/cancel"), MustHavePermission(AppAction.Cancel, AppFunction.Order)]
    public Task<OrderDto> Cancel(Guid id, CancelOrder request, CancellationToken ct) => ordering.CancelAsync(id, request, true, ct);
}
