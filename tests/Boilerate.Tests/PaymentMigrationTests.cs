using System.Text.Json;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.DigitalInventory;
using Boilerate.Domain.Ordering;
using Boilerate.Domain.Payments;
using Boilerate.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Boilerate.Tests;

[Collection("Foundation")]
public sealed class PaymentMigrationTests
{
    private static async Task<(Order Order, PaymentReceipt Receipt, DigitalDelivery Delivery)> Seed(ApplicationDbContext db)
    {
        var type = new ProductType("PAYMENT-MIGRATION", "Payment migration");
        var product = new Product(type.Id, "Unique account", "payment-migration", fulfillmentKind: ProductFulfillmentKind.DigitalUnique);
        var variant = product.AddVariant("PAYMENT-ACCOUNT", 100, null, new Dictionary<Guid, Guid>(), 200);
        var cart = new Cart(Guid.NewGuid());
        var item = new OrderItem(product.Id, variant.Id, product.Name, variant.Sku, "", ProductFulfillmentKind.DigitalUnique, 100, 1);
        var now = DateTime.UtcNow;
        var order = new Order(cart.CustomerId, cart.Id, Guid.NewGuid(), new string('A', 64), OrderPaymentMethod.Prepaid,
            "Migration customer", "customer@example.test", "0901234567", null, 0, now, 30, [item]);
        var stock = new DigitalStockItem(Guid.NewGuid(), product.Id, variant.Id, ProductFulfillmentKind.DigitalUnique,
            "ACCOUNT-ONE", "protected-stock-content-never-in-audit");
        stock.Reserve(item.Id);
        stock.Deliver(item.Id);
        order.RecordPayment(now);
        order.CompleteFulfillment(true);
        var receipt = new PaymentReceipt(order.Id, Guid.NewGuid(), new string('B', 64), "BANK-REF-1", order.Total, "VND", now, now);
        var delivery = new DigitalDelivery(Guid.NewGuid(), order.Id, item.Id, variant.Id, stock.Id, 1,
            ProductFulfillmentKind.DigitalUnique, "protected-delivery-content-never-in-audit");
        db.AddRange(type, product, cart, order, stock, receipt, delivery);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return (order, receipt, delivery);
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task DowngradeRefusesToDiscardReceiptsDeliveriesAndPermanentUniqueOwnership(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var seeded = await Seed(db);
        var before = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        var previous = db.Database.GetMigrations().Single(name => name.EndsWith("_OrderingCheckout", StringComparison.Ordinal));
        var blocked = await Record.ExceptionAsync(() => db.GetService<IMigrator>().MigrateAsync(previous));
        Assert.True(blocked is Microsoft.Data.SqlClient.SqlException { Number: 51000 } ||
            blocked is Npgsql.PostgresException { SqlState: "P0001" }, blocked?.ToString() ?? "Downgrade must preserve payment history.");
        Assert.Equal(before, (await db.Database.GetAppliedMigrationsAsync()).ToArray());
        Assert.Equal(seeded.Receipt.Id, (await db.PaymentReceipts.AsNoTracking().SingleAsync()).Id);
        Assert.Equal(seeded.Delivery.ProtectedContent, (await db.DigitalDeliveries.AsNoTracking().SingleAsync()).ProtectedContent);
        var stock = await db.DigitalStockItems.AsNoTracking().SingleAsync();
        Assert.Equal(DigitalStockState.Delivered, stock.State);
        Assert.Equal(seeded.Delivery.OrderItemId, stock.ReservedForOrderItemId);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task PaymentAndDeliveryHistoryIsImmutableSecretFreeInAuditAndBoundToTheCorrectKind(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var seeded = await Seed(db);
        var audit = JsonSerializer.Serialize(await db.Trails.AsNoTracking().ToListAsync());
        Assert.DoesNotContain(seeded.Delivery.ProtectedContent, audit);
        Assert.DoesNotContain("protected-stock-content-never-in-audit", audit);
        Assert.DoesNotContain("ProtectedContent", audit);
        Assert.Contains(seeded.Order.Id.ToString(), audit);

        (await db.PaymentReceipts.SingleAsync()).Touch();
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        db.PaymentReceipts.Remove(await db.PaymentReceipts.SingleAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        (await db.DigitalDeliveries.SingleAsync()).Touch();
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        db.DigitalDeliveries.Remove(await db.DigitalDeliveries.SingleAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        // A Shared label cannot bypass the Unique stock single-delivery index.
        db.DigitalDeliveries.Add(new(Guid.NewGuid(), seeded.Order.Id, seeded.Delivery.OrderItemId,
            seeded.Delivery.VariantId, seeded.Delivery.StockItemId, 2, ProductFulfillmentKind.DigitalShared, "another-protected-content"));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        db.DigitalDeliveries.Add(new(Guid.NewGuid(), seeded.Order.Id, seeded.Delivery.OrderItemId,
            seeded.Delivery.VariantId, seeded.Delivery.StockItemId, 2, ProductFulfillmentKind.DigitalUnique, "another-protected-content"));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        Assert.Single(await db.DigitalDeliveries.AsNoTracking().ToListAsync());
        Assert.Single(await db.PaymentReceipts.AsNoTracking().ToListAsync());
    }
}
