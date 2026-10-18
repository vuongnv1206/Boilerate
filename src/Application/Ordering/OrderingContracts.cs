using Boilerate.Application.Catalog;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Ordering;

namespace Boilerate.Application.Ordering;

public sealed record CartLineInput(Guid VariantId, int Quantity);
public sealed record SaveCart(string? Version, List<CartLineInput> Items);
public sealed record CartLineDto(Guid VariantId, int Quantity, string Name, string Sku, decimal? UnitPrice, bool IsAvailable);
public sealed record CartDto(Guid Id, string? Version, List<CartLineDto> Items, decimal Subtotal, decimal ShippingAmount = 0)
{
    public decimal Total => Subtotal + ShippingAmount;
    public bool CanCheckout => Items.Count > 0 && Total >= 0 && Total <= 9999999999999999m && decimal.Truncate(Total) == Total &&
        Items.All(i => i.IsAvailable && i.UnitPrice.HasValue);
}
public sealed record Checkout(string CartVersion, Guid OperationId, OrderPaymentMethod PaymentMethod,
    string RecipientName, string Email, string Phone, string? ShippingAddress, decimal ExpectedTotal);
public sealed record CancelOrder(string Version);
public sealed record OrderLineDto(Guid Id, Guid ProductId, Guid VariantId, string Name, string Sku, string OptionDescription,
    ProductFulfillmentKind FulfillmentKind, decimal UnitPrice, int Quantity);
public sealed record OrderDto(Guid Id, string Number, string Version, OrderStatus Status, OrderPaymentMethod PaymentMethod,
    decimal Subtotal, decimal ShippingAmount, decimal Total, string Currency, DateTime? ExpiresAt,
    string RecipientName, string Email, string Phone, string? ShippingAddress, List<OrderLineDto> Items);
public sealed record OrderSummary(Guid Id, string Number, OrderStatus Status, OrderPaymentMethod PaymentMethod, decimal Total,
    string Currency, DateTime CreatedOn, DateTime? ExpiresAt);

public interface IOrderingService
{
    Task<CartDto> GetCartAsync(CancellationToken ct);
    Task<CartDto> SaveCartAsync(SaveCart request, CancellationToken ct);
    Task<OrderDto> CheckoutAsync(Checkout request, CancellationToken ct);
    Task<OrderDto> GetOrderAsync(Guid id, bool admin, CancellationToken ct);
    Task<Page<OrderSummary>> OrdersAsync(int page, int size, bool admin, CancellationToken ct);
    Task<OrderDto> CancelAsync(Guid id, CancelOrder request, bool admin, CancellationToken ct);
    Task<int> ExpireAsync(CancellationToken ct);
}
