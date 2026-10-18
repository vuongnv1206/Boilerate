using Boilerate.Application.Ordering;
using Boilerate.Infrastructure.Auth.Permissions;
using Boilerate.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Boilerate.Host.Controllers.Catalog;

[Route("api/admin/orders"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PaymentsController(IPaymentService payments) : BaseApiController
{
    [HttpPost("{id:guid}/payment-confirmations"), MustHavePermission(AppAction.Confirm, AppFunction.Payment)]
    public Task<OrderDto> Confirm(Guid id, ConfirmPayment request, CancellationToken ct) => payments.ConfirmPaymentAsync(id, request, ct);

    [HttpPost("{id:guid}/retry-fulfillment"), MustHavePermission(AppAction.Fulfill, AppFunction.Order)]
    public Task<OrderDto> Retry(Guid id, RetryFulfillment request, CancellationToken ct) => payments.RetryFulfillmentAsync(id, request, ct);
}

[Authorize, Route("api/orders"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DigitalDeliveriesController(IPaymentService payments) : BaseApiController
{
    [HttpGet("{id:guid}/digital-deliveries")]
    public Task<List<DigitalDeliveryDto>> Get(Guid id, CancellationToken ct) => payments.DigitalDeliveriesAsync(id, ct);
}
