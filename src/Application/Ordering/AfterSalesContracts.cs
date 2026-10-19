namespace Boilerate.Application.Ordering;

public sealed record ConfirmRefund(string OrderVersion, Guid OperationId, string Reference, decimal Amount, string Currency,
    DateTime RefundedAtUtc, string Reason, string Source = "ManualBankTransfer");
public sealed record ReturnLineInput(Guid ShipmentId, Guid OrderItemId, int Quantity, int RestockQuantity);
public sealed record ReceiveReturn(string OrderVersion, Guid OperationId, string Reference, string Reason,
    List<ReturnLineInput> Items, bool CloseRemainingCod = false);
public sealed record RefundDto(Guid Id, Guid ReceiptId, string Reference, string Source, decimal Amount, string Currency,
    string Reason, DateTime RefundedAtUtc);
public sealed record ReturnLineDto(Guid ShipmentId, Guid OrderItemId, int Quantity, int RestockQuantity);
public sealed record PhysicalReturnDto(Guid Id, string Reference, string Reason, DateTime ReceivedAtUtc,
    bool CloseRemainingCod, List<ReturnLineDto> Items);
public sealed record RefundResult(RefundDto Refund, string OrderVersion, bool Closed);
public sealed record ReturnResult(PhysicalReturnDto Return, string OrderVersion, bool Closed);
public sealed record AfterSalesDto(Guid OrderId, string OrderVersion, bool Closed, decimal ReceivedAmount,
    decimal RefundedAmount, decimal RefundableAmount, List<RefundDto> Refunds, List<PhysicalReturnDto> Returns);

public interface IAfterSalesService
{
    Task<AfterSalesDto> AfterSalesAsync(Guid orderId, bool admin, CancellationToken ct);
    Task<RefundResult> ConfirmRefundAsync(Guid orderId, ConfirmRefund request, CancellationToken ct);
    Task<ReturnResult> ReceiveReturnAsync(Guid orderId, ReceiveReturn request, CancellationToken ct);
}
