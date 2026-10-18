namespace Boilerate.Application.Ordering;

public sealed record ConfirmPayment(string Version, Guid OperationId, string Reference, decimal Amount,
    string Currency, DateTime ReceivedAtUtc, string Source = "ManualBankTransfer");
public sealed record RetryFulfillment(string Version);
public sealed record DigitalDeliveryDto(Guid Id, Guid OrderItemId, int Sequence, string Content);

public interface IPaymentService
{
    Task<OrderDto> ConfirmPaymentAsync(Guid orderId, ConfirmPayment request, CancellationToken ct);
    Task<OrderDto> RetryFulfillmentAsync(Guid orderId, RetryFulfillment request, CancellationToken ct);
    Task<List<DigitalDeliveryDto>> DigitalDeliveriesAsync(Guid orderId, CancellationToken ct);
}
