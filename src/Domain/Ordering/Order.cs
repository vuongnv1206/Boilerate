using System.ComponentModel.DataAnnotations.Schema;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Contracts;
using Boilerate.Domain.Common.Exceptions;
namespace Boilerate.Domain.Ordering;

public enum OrderStatus
{
    PendingPayment = 1, AwaitingProcessing = 2, Cancelled = 3, Expired = 4
}

public enum OrderPaymentMethod
{
    Prepaid = 1, CashOnDelivery = 2
}

public sealed class Order : CommerceEntity, IAggregateRoot
{
    private readonly List<OrderItem> _items = [];
    public Guid CartId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid OperationId { get; private set; }
    public string RequestHash { get; private set; } = null!;
    public string Number { get; private set; } = null!;
    public OrderStatus Status { get; private set; }
    public OrderPaymentMethod PaymentMethod { get; private set; }
    public string Currency { get; private set; } = "VND";
    public decimal Subtotal { get; private set; }
    public decimal ShippingAmount { get; private set; }
    public decimal Total { get; private set; }
    public string RecipientName { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public string Phone { get; private set; } = null!;
    public string? ShippingAddress { get; private set; }
    public DateTime? ExpiresAt { get; private set; }
    public IReadOnlyCollection<OrderItem> Items => _items;

    private Order()
    {
    }

    public Order(Guid customerId, Guid cartId, Guid operationId, string requestHash, OrderPaymentMethod paymentMethod,
        string recipientName, string email, string phone, string? address, decimal shippingAmount, DateTime now,
        int reservationMinutes, IEnumerable<OrderItem> items)
    {
        if (customerId == Guid.Empty || cartId == Guid.Empty || operationId == Guid.Empty)
            throw new DomainException("Customer, cart and operation IDs are required.");
        if (requestHash is null || requestHash.Length != 64 || requestHash.Any(c => !char.IsAsciiHexDigit(c)))
            throw new DomainException("RequestHash must be a SHA-256 hexadecimal value.");
        if (!Enum.IsDefined(paymentMethod))
            throw new DomainException("Invalid payment method.");
        if (now.Kind != DateTimeKind.Utc || reservationMinutes is < 1 or > 1440 || now > DateTime.MaxValue.AddMinutes(-reservationMinutes))
            throw new DomainException("UTC time and a reservation duration from 1 to 1440 minutes are required.");
        if (items is null)
            throw new DomainException("Order items are required.");
        var snapshot = items.Take(51).ToList();
        if (snapshot.Count is < 1 or > 50 || snapshot.Any(i => i is null || i.OrderId != Guid.Empty) ||
            snapshot.Select(i => i.VariantId).Distinct().Count() != snapshot.Count)
            throw new DomainException("An order requires 1 to 50 distinct, unattached SKU snapshots.");
        var hasPhysical = snapshot.Any(i => i.FulfillmentKind == ProductFulfillmentKind.Physical);
        if (paymentMethod == OrderPaymentMethod.CashOnDelivery && snapshot.Any(i => i.FulfillmentKind != ProductFulfillmentKind.Physical))
            throw new DomainException("Orders containing digital products must be paid in advance.");
        CatalogRules.Money(shippingAmount, null);
        if (!hasPhysical && shippingAmount != 0)
            throw new DomainException("Digital-only orders cannot have a shipping charge.");
        var subtotal = snapshot.Sum(i => i.Amount);
        var total = subtotal + shippingAmount;
        CatalogRules.Money(subtotal, null);
        CatalogRules.Money(total, null);
        RecipientName = CatalogRules.Required(recipientName, 256, "RecipientName");
        Email = CatalogRules.Required(email, 256, "Email");
        if (!System.Net.Mail.MailAddress.TryCreate(Email, out var parsedEmail) || parsedEmail.Address != Email)
            throw new DomainException("A valid customer email address is required.");
        Phone = CatalogRules.Required(phone, 30, "Phone");
        ShippingAddress = hasPhysical ? CatalogRules.Required(address, 1000, "ShippingAddress") : null;
        CustomerId = customerId;
        CartId = cartId;
        OperationId = operationId;
        RequestHash = requestHash;
        Number = Id.ToString("N").ToUpperInvariant();
        PaymentMethod = paymentMethod;
        Subtotal = subtotal;
        ShippingAmount = shippingAmount;
        Total = total;
        Status = paymentMethod == OrderPaymentMethod.Prepaid ? OrderStatus.PendingPayment : OrderStatus.AwaitingProcessing;
        ExpiresAt = paymentMethod == OrderPaymentMethod.Prepaid ? now.AddMinutes(reservationMinutes) : null;
        foreach (var item in snapshot)
        {
            item.Attach(Id);
            _items.Add(item);
        }
    }

    public void Cancel()
    {
        if (Status is not OrderStatus.PendingPayment and not OrderStatus.AwaitingProcessing)
            throw new DomainException("Only pending orders can be cancelled.");
        Status = OrderStatus.Cancelled;
        Touch();
    }

    public bool Expire(DateTime now)
    {
        if (now.Kind != DateTimeKind.Utc)
            throw new DomainException("Expiry checks require UTC time.");
        if (Status != OrderStatus.PendingPayment || !ExpiresAt.HasValue || now < ExpiresAt.Value)
            return false;
        Status = OrderStatus.Expired;
        Touch();
        return true;
    }
}

public sealed class OrderItem : BaseEntity
{
    public Guid OrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid VariantId { get; private set; }
    public string Name { get; private set; } = null!;
    public string Sku { get; private set; } = null!;
    public string OptionDescription { get; private set; } = null!;
    public ProductFulfillmentKind FulfillmentKind { get; private set; }
    public decimal UnitPrice { get; private set; }
    public int Quantity { get; private set; }
    [NotMapped]
    public decimal Amount => UnitPrice * Quantity;

    private OrderItem()
    {
    }

    public OrderItem(Guid productId, Guid variantId, string name, string sku, string optionDescription,
        ProductFulfillmentKind kind, decimal unitPrice, int quantity)
    {
        if (productId == Guid.Empty || variantId == Guid.Empty || quantity is < 1 or > 999 || !Enum.IsDefined(kind))
            throw new DomainException("Order snapshots require valid product/SKU IDs, fulfillment kind and quantity from 1 to 999.");
        if (optionDescription is null || optionDescription.Length > 2000)
            throw new DomainException("Option description must not exceed 2000 characters.");
        CatalogRules.Money(unitPrice, null);
        CatalogRules.Money(unitPrice * quantity, null);
        ProductId = productId;
        VariantId = variantId;
        Name = CatalogRules.Required(name, 256, "Name");
        Sku = CatalogRules.Required(sku, 100, "Sku");
        OptionDescription = optionDescription;
        FulfillmentKind = kind;
        UnitPrice = unitPrice;
        Quantity = quantity;
    }

    internal void Attach(Guid orderId)
    {
        if (orderId == Guid.Empty || OrderId != Guid.Empty)
            throw new DomainException("An order item may only be attached once.");
        OrderId = orderId;
    }
}
