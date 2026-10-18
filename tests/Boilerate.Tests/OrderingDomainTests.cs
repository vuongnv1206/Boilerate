using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using Boilerate.Domain.DigitalInventory;
using Boilerate.Domain.Inventory;
using Boilerate.Domain.Ordering;
using Xunit;
namespace Boilerate.Tests;

public sealed class OrderingDomainTests
{
    private static readonly DateTime CheckoutTime = new(2026, 10, 18, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CartAcceptsEmptyAndReplacesItemsWhileRejectingInvalidReplacementAtomically()
    {
        var cart = new Cart(Guid.NewGuid());
        var variant = Guid.NewGuid();
        var initialVersion = cart.Version;
        cart.ReplaceItems([(variant, 2)]);
        Assert.NotEqual(initialVersion, cart.Version);
        var item = Assert.Single(cart.Items);
        Assert.Equal(cart.Id, item.CartId);
        Assert.Equal(2, item.Quantity);
        var version = cart.Version;
        Assert.Throws<DomainException>(() => cart.ReplaceItems([(variant, 1), (variant, 2)]));
        Assert.Equal(version, cart.Version);
        Assert.Same(item, Assert.Single(cart.Items));
        cart.ReplaceItems([(variant, 4)]);
        Assert.Same(item, Assert.Single(cart.Items));
        Assert.Equal(4, item.Quantity);
        Assert.NotEqual(version, cart.Version);
        version = cart.Version;
        cart.ReplaceItems([]);
        Assert.Empty(cart.Items);
        Assert.NotEqual(version, cart.Version);
    }

    [Fact]
    public void CartGuardsCustomerSkuQuantityAndMaximumDistinctItems()
    {
        Assert.Throws<DomainException>(() => new Cart(Guid.Empty));
        var cart = new Cart(Guid.NewGuid());
        Assert.Throws<DomainException>(() => cart.ReplaceItems([(Guid.Empty, 1)]));
        Assert.Throws<DomainException>(() => cart.ReplaceItems([(Guid.NewGuid(), 0)]));
        Assert.Throws<DomainException>(() => cart.ReplaceItems([(Guid.NewGuid(), 1000)]));
        Assert.Throws<DomainException>(() => cart.ReplaceItems(Enumerable.Range(0, 51).Select(_ => (Guid.NewGuid(), 1))));
        cart.ReplaceItems(Enumerable.Range(0, 50).Select(_ => (Guid.NewGuid(), 999)));
        Assert.Equal(50, cart.Items.Count);
    }

    [Fact]
    public void PrepaidMixedOrderSnapshotsTotalsAndExpiresAtTheConfiguredBoundary()
    {
        var physical = Item(ProductFulfillmentKind.Physical, 100000, 2);
        var code = Item(ProductFulfillmentKind.DigitalUnique, 50000, 1);
        var order = NewOrder([physical, code], shipping: 15000);
        Assert.Equal(OrderStatus.PendingPayment, order.Status);
        Assert.Equal(OrderPaymentMethod.Prepaid, order.PaymentMethod);
        Assert.Equal("VND", order.Currency);
        Assert.Equal(250000m, order.Subtotal);
        Assert.Equal(265000m, order.Total);
        Assert.Equal(CheckoutTime.AddMinutes(30), order.ExpiresAt);
        Assert.All(order.Items, i => Assert.Equal(order.Id, i.OrderId));
        var version = order.Version;
        Assert.False(order.Expire(CheckoutTime.AddMinutes(30).AddTicks(-1)));
        Assert.Equal(version, order.Version);
        Assert.True(order.Expire(CheckoutTime.AddMinutes(30)));
        Assert.Equal(OrderStatus.Expired, order.Status);
        Assert.NotEqual(version, order.Version);
        version = order.Version;
        Assert.False(order.Expire(CheckoutTime.AddHours(2)));
        Assert.Equal(version, order.Version);
        Assert.Throws<DomainException>(order.Cancel);
    }

    [Fact]
    public void CodOnlyAcceptsPhysicalProductsRequiresAddressAndHasNoPaymentExpiry()
    {
        var order = NewOrder([Item()], OrderPaymentMethod.CashOnDelivery);
        Assert.Equal(OrderStatus.AwaitingProcessing, order.Status);
        Assert.Null(order.ExpiresAt);
        Assert.False(order.Expire(CheckoutTime.AddDays(1)));
        order.Cancel();
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Throws<DomainException>(order.Cancel);
        Assert.Throws<DomainException>(() => NewOrder([Item(ProductFulfillmentKind.DigitalUnique)], OrderPaymentMethod.CashOnDelivery));
        Assert.Throws<DomainException>(() => NewOrder([Item(ProductFulfillmentKind.DigitalShared)], OrderPaymentMethod.CashOnDelivery));
        Assert.Throws<DomainException>(() => NewOrder([Item(ProductFulfillmentKind.ManualExtension)], OrderPaymentMethod.CashOnDelivery));
        Assert.Throws<DomainException>(() => NewOrder([Item()], address: " "));
    }

    [Fact]
    public void DigitalOnlyOrderDoesNotRequireAddressOrShippingAndCancellationPreventsExpiry()
    {
        var order = NewOrder([Item(ProductFulfillmentKind.DigitalShared, 10000, 2)], address: null);
        Assert.Null(order.ShippingAddress);
        Assert.Equal(20000m, order.Total);
        Assert.Throws<DomainException>(() => NewOrder([Item(ProductFulfillmentKind.DigitalShared)], shipping: 1));
        order.Cancel();
        var version = order.Version;
        Assert.False(order.Expire(CheckoutTime.AddHours(1)));
        Assert.Equal(version, order.Version);
    }

    [Fact]
    public void OrderCannotBeEmptyContainDuplicateSkusOrReuseAnAttachedSnapshot()
    {
        Assert.Throws<DomainException>(() => NewOrder([]));
        var variant = Guid.NewGuid();
        var first = new OrderItem(Guid.NewGuid(), variant, "Shirt", "SHIRT", "Color: black", ProductFulfillmentKind.Physical, 10000, 1);
        var duplicate = new OrderItem(Guid.NewGuid(), variant, "Shirt", "SHIRT", "Color: black", ProductFulfillmentKind.Physical, 10000, 2);
        Assert.Throws<DomainException>(() => NewOrder([first, duplicate]));
        Assert.Equal(Guid.Empty, first.OrderId);
        var order = NewOrder([first]);
        Assert.Throws<DomainException>(() => NewOrder([first]));
        Assert.Equal(order.Id, first.OrderId);
        Assert.Throws<DomainException>(() => NewOrder(Enumerable.Range(0, 51).Select(_ => Item()).ToArray()));
    }

    [Fact]
    public void OrderMoneyAndSnapshotBoundsPreventFractionalNegativeOrUnpersistableTotals()
    {
        Assert.Throws<DomainException>(() => Item(price: 1.5m));
        Assert.Throws<DomainException>(() => Item(price: -1));
        Assert.Throws<DomainException>(() => Item(quantity: 0));
        Assert.Throws<DomainException>(() => Item(quantity: 1000));
        Assert.Throws<DomainException>(() => Item(price: 9999999999999999m, quantity: 2));
        Assert.Throws<DomainException>(() => NewOrder([Item(price: 9999999999999999m), Item(price: 1)]));
        Assert.Throws<DomainException>(() => NewOrder([Item()], shipping: 0.5m));
        Assert.Throws<DomainException>(() => NewOrder([Item()], shipping: -1));
        Assert.Throws<DomainException>(() => new OrderItem(Guid.Empty, Guid.NewGuid(), "Name", "SKU", "", ProductFulfillmentKind.Physical, 100, 1));
        Assert.Throws<DomainException>(() => new OrderItem(Guid.NewGuid(), Guid.NewGuid(), "Name", "SKU", new string('x', 2001), ProductFulfillmentKind.Physical, 100, 1));
    }

    [Fact]
    public void OrderRejectsInvalidIdentityHashMethodAndReservationDurationWithoutAttachingItems()
    {
        var item = Item();
        Assert.Throws<DomainException>(() => new Order(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), new string('a', 64), OrderPaymentMethod.Prepaid, "Name", "a@example.com", "123", "Address", 0, CheckoutTime, 30, [item]));
        Assert.Throws<DomainException>(() => new Order(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "invalid", OrderPaymentMethod.Prepaid, "Name", "a@example.com", "123", "Address", 0, CheckoutTime, 30, [item]));
        Assert.Throws<DomainException>(() => new Order(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new string('a', 64), OrderPaymentMethod.Prepaid, "Name", "invalid email", "123", "Address", 0, CheckoutTime, 30, [item]));
        Assert.Throws<DomainException>(() => NewOrder([item], (OrderPaymentMethod)99));
        Assert.Throws<DomainException>(() => NewOrder([item], minutes: 0));
        Assert.Throws<DomainException>(() => NewOrder([item], minutes: 1441));
        Assert.Equal(Guid.Empty, item.OrderId);
        var order = NewOrder([item], minutes: 1);
        Assert.Equal(CheckoutTime.AddMinutes(1), order.ExpiresAt);
        Assert.Throws<DomainException>(() => order.Expire(DateTime.SpecifyKind(CheckoutTime, DateTimeKind.Unspecified)));
    }

    [Fact]
    public void ReservationsReduceAvailabilityAndPreventAdjustmentBelowAlreadyPromisedInventory()
    {
        var balance = new InventoryItem(Guid.NewGuid(), Guid.NewGuid());
        balance.Adjust(Guid.NewGuid(), 3, "Opening", Guid.NewGuid());
        var version = balance.Version;
        balance.Reserve(2);
        Assert.Equal(3, balance.OnHand);
        Assert.Equal(2, balance.Reserved);
        Assert.Equal(1, balance.Available);
        Assert.NotEqual(version, balance.Version);
        version = balance.Version;
        Assert.Throws<DomainException>(() => balance.Reserve(2));
        Assert.Throws<DomainException>(() => balance.Reserve(0));
        Assert.Throws<DomainException>(() => balance.Adjust(Guid.NewGuid(), -2, "Would consume held stock", Guid.NewGuid()));
        Assert.Equal(version, balance.Version);
        Assert.Equal(3, balance.OnHand);
        balance.Adjust(Guid.NewGuid(), -1, "Available unit removed", Guid.NewGuid());
        Assert.Equal(0, balance.Available);
        balance.Release(2);
        Assert.Equal(0, balance.Reserved);
        Assert.Equal(2, balance.Available);
        Assert.Throws<DomainException>(() => balance.Release(1));
        Assert.Throws<DomainException>(() => balance.Release(0));
    }

    [Fact]
    public void PhysicalHoldReleasesOnlyOnceWithoutRotatingVersionOnRetry()
    {
        var hold = new PhysicalStockReservation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 2);
        Assert.Equal(HoldState.Held, hold.State);
        var version = hold.Version;
        Assert.True(hold.Release());
        Assert.Equal(HoldState.Released, hold.State);
        Assert.NotEqual(version, hold.Version);
        version = hold.Version;
        Assert.False(hold.Release());
        Assert.Equal(version, hold.Version);
        Assert.Throws<DomainException>(() => new PhysicalStockReservation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0));
    }

    [Fact]
    public void UniqueDigitalHoldRejectsOtherOwnersAndAvailabilityChangesThenCanBeReleasedOnce()
    {
        var stock = Stock(ProductFulfillmentKind.DigitalUnique, "CODE");
        var orderItem = Guid.NewGuid();
        stock.Reserve(orderItem);
        Assert.Equal(DigitalStockState.Reserved, stock.State);
        Assert.Equal(orderItem, stock.ReservedForOrderItemId);
        var version = stock.Version;
        Assert.Throws<DomainException>(() => stock.Reserve(Guid.NewGuid()));
        Assert.Throws<DomainException>(() => stock.SetAvailability(false));
        Assert.Throws<DomainException>(() => stock.SetAvailability(true));
        Assert.Throws<DomainException>(() => stock.Release(Guid.NewGuid()));
        Assert.Equal(version, stock.Version);
        Assert.True(stock.Release(orderItem));
        Assert.Equal(DigitalStockState.Available, stock.State);
        Assert.Null(stock.ReservedForOrderItemId);
        version = stock.Version;
        Assert.False(stock.Release(orderItem));
        Assert.Equal(version, stock.Version);
        stock.Reserve(Guid.NewGuid());
        Assert.Throws<DomainException>(() => stock.Release(orderItem));
    }

    [Fact]
    public void SharedAndDisabledDigitalContentCannotBeExclusivelyReserved()
    {
        var shared = Stock(ProductFulfillmentKind.DigitalShared, "SHARED");
        Assert.Throws<DomainException>(() => shared.Reserve(Guid.NewGuid()));
        Assert.Throws<DomainException>(() => shared.Release(Guid.NewGuid()));
        var unique = Stock(ProductFulfillmentKind.DigitalUnique, "CODE");
        Assert.Throws<DomainException>(() => unique.Reserve(Guid.Empty));
        unique.SetAvailability(false);
        Assert.Throws<DomainException>(() => unique.Reserve(Guid.NewGuid()));
        Assert.Throws<DomainException>(() => unique.Release(Guid.NewGuid()));
        Assert.Equal(DigitalStockState.Disabled, unique.State);
    }

    private static OrderItem Item(ProductFulfillmentKind kind = ProductFulfillmentKind.Physical, decimal price = 10000, int quantity = 1) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Product", "SKU", "", kind, price, quantity);

    private static Order NewOrder(OrderItem[] items, OrderPaymentMethod method = OrderPaymentMethod.Prepaid,
        decimal shipping = 0, string? address = "1 Street", int minutes = 30) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new string('a', 64), method,
            "Customer", "customer@example.com", "0123456789", address, shipping, CheckoutTime, minutes, items);

    private static DigitalStockItem Stock(ProductFulfillmentKind kind, string reference) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), kind, reference, "protected-content");
}
