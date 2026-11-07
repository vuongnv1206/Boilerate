using Boilerate.Domain.Catalog;
using Boilerate.Domain.Ordering;

namespace Boilerate.Application.Ordering.Shared;

public sealed record Checkout(
    string CartVersion,
    Guid OperationId,
    OrderPaymentMethod PaymentMethod,
    string RecipientName,
    string Email,
    string Phone,
    string? ShippingAddress,
    decimal ExpectedTotal);

public sealed record CancelOrder(string Version);

public sealed record OrderLineDto(
    Guid Id,
    Guid ProductId,
    Guid VariantId,
    string Name,
    string Sku,
    string OptionDescription,
    ProductFulfillmentKind FulfillmentKind,
    decimal UnitPrice,
    int Quantity,
    decimal DiscountAmount = 0);

public sealed record OrderDto(
    Guid Id,
    string Number,
    string Version,
    OrderStatus Status,
    OrderPaymentMethod PaymentMethod,
    decimal Subtotal,
    decimal ShippingAmount,
    decimal Total,
    string Currency,
    DateTime? ExpiresAt,
    string RecipientName,
    string Email,
    string Phone,
    string? ShippingAddress,
    List<OrderLineDto> Items,
    OrderPaymentStatus PaymentStatus = OrderPaymentStatus.Unpaid,
    OrderFulfillmentStatus FulfillmentStatus = OrderFulfillmentStatus.Pending,
    DateTime? PaidAtUtc = null,
    DateTime CreatedOn = default,
    bool PhysicalDispatchStarted = false,
    string? DiscountCode = null,
    decimal DiscountAmount = 0);

public sealed record OrderSummary(
    Guid Id,
    string Number,
    OrderStatus Status,
    OrderPaymentMethod PaymentMethod,
    decimal Total,
    string Currency,
    DateTime CreatedOn,
    DateTime? ExpiresAt,
    OrderPaymentStatus PaymentStatus = OrderPaymentStatus.Unpaid,
    OrderFulfillmentStatus FulfillmentStatus = OrderFulfillmentStatus.Pending,
    DateTime? PaidAtUtc = null,
    string RecipientName = "",
    string Email = "",
    string Phone = "");

public sealed class AdminOrderQuery
{
    public int Page { get; init; } = 1;

    public int Size { get; init; } = 20;

    public string? Search { get; init; }

    public string? Queue { get; init; }

    public OrderStatus? Status { get; init; }

    public OrderPaymentStatus? PaymentStatus { get; init; }

    public OrderFulfillmentStatus? FulfillmentStatus { get; init; }

    public DateTime? CreatedFrom { get; init; }

    public DateTime? CreatedTo { get; init; }
}

public sealed record OrderHistoryEntryDto(
    Guid Id,
    DateTime OccurredAtUtc,
    string Action,
    string? ActorDisplayName,
    string RelatedEntityType,
    string SafeSummary);

