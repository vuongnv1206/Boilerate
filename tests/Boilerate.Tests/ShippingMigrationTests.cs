using System.Text.Json;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Inventory;
using Boilerate.Domain.Ordering;
using Boilerate.Domain.Shipping;
using Boilerate.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Boilerate.Tests;

[Collection("Foundation")]
public sealed class ShippingMigrationTests
{
    private static async Task AssertConstraintAsync(Func<Task> action)
    {
        var exception = await Record.ExceptionAsync(action);
        Assert.True(exception is Microsoft.Data.SqlClient.SqlException { Number: 547 or 2601 or 2627 } ||
            exception is Npgsql.PostgresException { SqlState: "23503" or "23505" or "23514" },
            exception?.ToString() ?? "Expected a database FK, unique or check constraint violation.");
    }

    private static async Task<(Order Order, Shipment Shipment, PhysicalStockReservation Reservation)> Seed(ApplicationDbContext db)
    {
        var type = new ProductType("SHIPPING-MIGRATION", "Shipping migration");
        var product = new Product(type.Id, "Physical product", "shipping-migration");
        var variant = product.AddVariant("SHIPPING-SKU", 100, null, new Dictionary<Guid, Guid>(), 200);
        var location = new InventoryLocation("MAIN", "Main warehouse");
        var balance = new InventoryItem(variant.Id, location.Id);
        var opening = balance.Adjust(Guid.NewGuid(), 3, "Opening stock", Guid.Empty);
        var cart = new Cart(Guid.NewGuid());
        var item = new OrderItem(product.Id, variant.Id, product.Name, variant.Sku, "", ProductFulfillmentKind.Physical, 100, 2);
        var now = DateTime.UtcNow;
        var order = new Order(cart.CustomerId, cart.Id, Guid.NewGuid(), new string('A', 64), OrderPaymentMethod.CashOnDelivery,
            "Migration customer", "customer@example.test", "0901234567", "123 Test Street", 0, now, 30, [item]);
        balance.Reserve(2);
        var reservation = new PhysicalStockReservation(item.Id, variant.Id, balance.Id, 2);
        var movement = balance.ConsumeReserved(Guid.NewGuid(), 1, "Shipment dispatch", Guid.NewGuid());
        reservation.Consume(1);
        var shipment = new Shipment(order.Id, Guid.NewGuid(), new string('B', 64), "Manual carrier", "TRACK-ONE", now,
            [new(order.Id, item.Id, variant.Id, movement.Id, 1)]);
        order.RegisterPhysicalShipment(false);
        db.AddRange(type, product, location, balance, opening, cart, order, reservation, movement, shipment);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return (order, shipment, reservation);
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task ShipmentItemsAreImmutableBoundToTheirOrderAndKeepLedgerAndAuditHistory(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var seeded = await Seed(db);
        var item = seeded.Order.Items.Single();
        var shipmentItem = await db.ShipmentItems.SingleAsync();
        var audit = JsonSerializer.Serialize(await db.Trails.AsNoTracking().Where(x => x.TableName == "ShipmentItems").ToListAsync());
        Assert.Contains(seeded.Shipment.Id.ToString(), audit);
        Assert.Contains(item.Id.ToString(), audit);
        Assert.Contains(shipmentItem.StockMovementId.ToString(), audit);

        db.Entry(shipmentItem).Property(x => x.Quantity).CurrentValue = 2;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        db.ShipmentItems.Remove(await db.ShipmentItems.SingleAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        db.Shipments.Remove(await db.Shipments.SingleAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        await AssertConstraintAsync(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Ordering"."PhysicalStockReservations" SET "ConsumedQuantity" = 3 WHERE "Id" = {seeded.Reservation.Id}
            """));
        await AssertConstraintAsync(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Shipping"."Shipments" SET "Status" = 2 WHERE "Id" = {seeded.Shipment.Id}
            """));
        await AssertConstraintAsync(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Shipping"."ShipmentItems" SET "FulfillmentKind" = 2 WHERE "ShipmentId" = {seeded.Shipment.Id}
            """));

        // A valid order/item from another order must still fail the shipment/order composite FK.
        var otherCart = new Cart(Guid.NewGuid());
        var otherItem = new OrderItem(item.ProductId, item.VariantId, item.Name, item.Sku, "", item.FulfillmentKind, 100, 1);
        var otherOrder = new Order(otherCart.CustomerId, otherCart.Id, Guid.NewGuid(), new string('C', 64), OrderPaymentMethod.CashOnDelivery,
            "Second customer", "second@example.test", "0901234568", "456 Test Street", 0, DateTime.UtcNow, 30, [otherItem]);
        db.AddRange(otherCart, otherOrder);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var unusedMovementId = await db.StockMovements.Where(x => x.Delta > 0).Select(x => x.Id).SingleAsync();
        await AssertConstraintAsync(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Shipping"."ShipmentItems" ("ShipmentId", "OrderId", "OrderItemId", "VariantId", "StockMovementId", "FulfillmentKind", "Quantity")
            VALUES ({seeded.Shipment.Id}, {otherOrder.Id}, {otherItem.Id}, {item.VariantId}, {unusedMovementId}, 1, 1)
            """));

        Assert.Equal(1, (await db.ShipmentItems.AsNoTracking().SingleAsync()).Quantity);
        Assert.Equal(2, (await db.InventoryItems.AsNoTracking().SingleAsync()).OnHand);
        Assert.Equal(1, (await db.InventoryItems.AsNoTracking().SingleAsync()).Reserved);
        Assert.Equal(1, (await db.PhysicalStockReservations.AsNoTracking().SingleAsync()).ConsumedQuantity);
        Assert.Equal(-1, (await db.StockMovements.AsNoTracking().SingleAsync(x => x.Id == shipmentItem.StockMovementId)).Delta);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task EmptyMigrationCanRoundtripButDispatchHistoryBlocksDowngradeBeforeAnyDdl(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var migrator = db.GetService<IMigrator>();
        var previous = db.Database.GetMigrations().Single(x => x.EndsWith("_PaymentDigitalDelivery", StringComparison.Ordinal));
        var before = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        Assert.EndsWith("_RefundsReturns", before.Last());
        await migrator.MigrateAsync(previous);
        Assert.Equal(previous, (await db.Database.GetAppliedMigrationsAsync()).Last());
        await migrator.MigrateAsync();
        Assert.Equal(before, (await db.Database.GetAppliedMigrationsAsync()).ToArray());
        Assert.False(db.Database.HasPendingModelChanges());

        var seeded = await Seed(db);
        var blocked = await Record.ExceptionAsync(() => migrator.MigrateAsync(previous));
        Assert.True(blocked is Microsoft.Data.SqlClient.SqlException { Number: 51000 } ||
            blocked is Npgsql.PostgresException { SqlState: "P0001" }, blocked?.ToString() ?? "Downgrade must retain shipment/stock history.");
        Assert.Equal(before, (await db.Database.GetAppliedMigrationsAsync()).ToArray());
        Assert.Equal(seeded.Shipment.Id, (await db.Shipments.AsNoTracking().SingleAsync()).Id);
        Assert.Equal(1, (await db.PhysicalStockReservations.AsNoTracking().SingleAsync()).ConsumedQuantity);
        Assert.True((await db.Orders.AsNoTracking().SingleAsync()).PhysicalDispatchStarted);
    }
}
