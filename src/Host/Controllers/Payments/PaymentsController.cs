using Boilerate.Application.Ordering.Shared;
using Boilerate.Application.Payments;
using Boilerate.Infrastructure.Auth.Permissions;
using Boilerate.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Boilerate.Host.Controllers.Payments;

[Route("api/admin/orders"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PaymentsController : BaseApiController
{
    [HttpPost("{id:guid}/payment-confirmations"), MustHavePermission(AppAction.Confirm, AppFunction.Payment)]
    public Task<OrderDto> Confirm(Guid id, ConfirmPayment request, CancellationToken ct) =>
        Mediator.Send(new ConfirmPaymentRequest(id, request), ct);

    [HttpPost("{id:guid}/retry-fulfillment"), MustHavePermission(AppAction.Fulfill, AppFunction.Order)]
    public Task<OrderDto> Retry(Guid id, RetryFulfillment request, CancellationToken ct) =>
        Mediator.Send(new RetryOrderFulfillmentRequest(id, request), ct);
}

[Authorize, Route("api/orders"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DigitalDeliveriesController : BaseApiController
{
    [HttpGet("{id:guid}/digital-deliveries")]
    public Task<List<DigitalDeliveryDto>> Get(Guid id, CancellationToken ct) =>
        Mediator.Send(new GetMyDigitalDeliveriesRequest(id), ct);
}

