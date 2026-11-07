using Boilerate.Domain.Catalog;

namespace Boilerate.Application.Ordering.Shared;

public sealed record CartLineInput(Guid VariantId, int Quantity);

public sealed record SaveCart(string? Version, List<CartLineInput> Items);

public sealed record SetCartDiscount(string? Version, string? Code);

public sealed record CartLineDto(
    Guid VariantId,
    int Quantity,
    string Name,
    string Sku,
    decimal? UnitPrice,
    bool IsAvailable,
    ProductFulfillmentKind FulfillmentKind = ProductFulfillmentKind.Physical);

public sealed record CartDto(
    Guid Id,
    string? Version,
    List<CartLineDto> Items,
    decimal Subtotal,
    decimal ShippingAmount = 0,
    string? DiscountCode = null,
    decimal DiscountAmount = 0,
    string? DiscountError = null)
{
    public decimal Total => Subtotal + ShippingAmount - DiscountAmount;

    public bool CanCheckout =>
        DiscountError is null &&
        Items.Count > 0 &&
        Total >= 0 &&
        Total <= 9999999999999999m &&
        decimal.Truncate(Total) == Total &&
        Items.All(i => i.IsAvailable && i.UnitPrice.HasValue);

    public bool HasDigitalItems => Items.Any(i => i.FulfillmentKind != ProductFulfillmentKind.Physical);
}

