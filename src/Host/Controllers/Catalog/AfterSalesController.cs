using Boilerate.Application.Ordering;
using Boilerate.Infrastructure.Auth.Permissions;
using Boilerate.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Boilerate.Host.Controllers.Catalog;

[Authorize, Route("api/orders/{orderId:guid}/after-sales"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class MyAfterSalesController(IAfterSalesService afterSales) : BaseApiController
{
    [HttpGet]
    public Task<AfterSalesDto> Get(Guid orderId, CancellationToken ct) => afterSales.AfterSalesAsync(orderId, false, ct);
}

[Route("api/admin/orders/{orderId:guid}"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminAfterSalesController(IAfterSalesService afterSales) : BaseApiController
{
    [HttpGet("after-sales"), MustHavePermission(AppAction.View, AppFunction.AfterSales)]
    public Task<AfterSalesDto> Get(Guid orderId, CancellationToken ct) => afterSales.AfterSalesAsync(orderId, true, ct);
    [HttpPost("refunds"), MustHavePermission(AppAction.Confirm, AppFunction.Refund)]
    public Task<RefundResult> Refund(Guid orderId, ConfirmRefund request, CancellationToken ct) => afterSales.ConfirmRefundAsync(orderId, request, ct);
    [HttpPost("returns"), MustHavePermission(AppAction.Receive, AppFunction.Return)]
    public Task<ReturnResult> Receive(Guid orderId, ReceiveReturn request, CancellationToken ct) => afterSales.ReceiveReturnAsync(orderId, request, ct);
}
