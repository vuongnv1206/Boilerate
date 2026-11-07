using Boilerate.Application.AfterSales;
using Boilerate.Infrastructure.Auth.Permissions;
using Boilerate.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Boilerate.Host.Controllers.AfterSales;

[Authorize, Route("api/orders/{orderId:guid}/after-sales"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class MyAfterSalesController : BaseApiController
{
    [HttpGet]
    public Task<AfterSalesDto> Get(Guid orderId, CancellationToken ct) =>
        Mediator.Send(new GetMyOrderAfterSalesRequest(orderId), ct);
}

[Route("api/admin/orders/{orderId:guid}"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminAfterSalesController : BaseApiController
{
    [HttpGet("after-sales"), MustHavePermission(AppAction.View, AppFunction.AfterSales)]
    public Task<AfterSalesDto> Get(Guid orderId, CancellationToken ct) =>
        Mediator.Send(new GetAdminOrderAfterSalesRequest(orderId), ct);

    [HttpPost("refunds"), MustHavePermission(AppAction.Confirm, AppFunction.Refund)]
    public Task<RefundResult> Refund(Guid orderId, ConfirmRefund request, CancellationToken ct) =>
        Mediator.Send(new ConfirmRefundRequest(orderId, request), ct);

    [HttpPost("returns"), MustHavePermission(AppAction.Receive, AppFunction.Return)]
    public Task<ReturnResult> Receive(Guid orderId, ReceiveReturn request, CancellationToken ct) =>
        Mediator.Send(new ReceivePhysicalReturnRequest(orderId, request), ct);
}

