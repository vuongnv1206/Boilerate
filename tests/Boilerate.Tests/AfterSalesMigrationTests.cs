using System.Text.Json;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Inventory;
using Boilerate.Domain.Ordering;
using Boilerate.Domain.Payments;
using Boilerate.Domain.Shipping;
using Boilerate.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Boilerate.Tests;

[Collection("Foundation")]
public sealed class AfterSalesMigrationTests
{
    private static readonly DateTime Now = new(2026, 10, 19, 8, 0, 0, DateTimeKind.Utc);

    private static async Task AssertConstraintAsync(Func<Task> action)
    {
        var exception = await Record.ExceptionAsync(action);
        Assert.True(exception is Microsoft.Data.SqlClient.SqlException { Number: 547 or 2601 or 2627 } ||
            exception is Npgsql.PostgresException { SqlState: "23503" or "23505" or "23514" },
            exception?.ToString() ?? "Expected a database FK, unique or check constraint violation.");
    }

    private static async Task<(Order Order, Order Other, PaymentReceipt Receipt, Shipment Shipment,
        Refund Refund, PhysicalReturn Return, PhysicalStockReservation Hold)> Seed(ApplicationDbContext db)
    {
        var type = new ProductType("AFTERSALES-MIGRATION", "After-sales migration");
        var product = new Product(type.Id, "Physical product", "aftersales-migration");
        var variant = product.AddVariant("AFTERSALES-SKU", 100, null, new Dictionary<Guid, Guid>(), 200);
        var otherProduct = new Product(type.Id, "Other physical product", "aftersales-other");
        var otherVariant = otherProduct.AddVariant("AFTERSALES-OTHER-SKU", 100, null, new Dictionary<Guid, Guid>(), 200);
        var location = new InventoryLocation("MAIN", "Main warehouse");
        var balance = new InventoryItem(variant.Id, location.Id);
        var opening = balance.Adjust(Guid.NewGuid(), 3, "Opening stock", Guid.NewGuid());
        Order NewOrder(Product selectedProduct, ProductVariant selectedVariant)
        {
            var cart = new Cart(Guid.NewGuid());
            var item = new OrderItem(selectedProduct.Id, selectedVariant.Id, selectedProduct.Name, selectedVariant.Sku, "", ProductFulfillmentKind.Physical, 100, 2);
            var order = new Order(cart.CustomerId, cart.Id, Guid.NewGuid(), new string('A', 64), OrderPaymentMethod.CashOnDelivery,
                "Migration customer", "customer@example.test", "0901234567", "123 Test Street", 0, Now, 30, [item]);
            db.AddRange(cart, order);
            return order;
        }
        var order = NewOrder(product, variant);
        var other = NewOrder(otherProduct, otherVariant);
        var orderItem = order.Items.Single();
        var receipt = new PaymentReceipt(order.Id, Guid.NewGuid(), new string('B', 64), "RECEIPT-ONE", 200, "VND", Now, Now);
        order.RecordPayment(Now);
        balance.Reserve(2);
        var hold = new PhysicalStockReservation(orderItem.Id, variant.Id, balance.Id, 2);
        var outbound = balance.ConsumeReserved(Guid.NewGuid(), 1, "Shipment dispatch", Guid.NewGuid());
        hold.Consume(1);
        var shipment = new Shipment(order.Id, Guid.NewGuid(), new string('C', 64), "Manual carrier", "TRACK-ONE", Now,
            [new(order.Id, orderItem.Id, variant.Id, outbound.Id, 1)]);
        order.RegisterPhysicalShipment(false);
        var refund = new Refund(receipt.Id, order.Id, Guid.NewGuid(), new string('D', 64), "REFUND-ONE",
            50, "VND", Now, Now, Now, "Verified transfer", "ManualBankTransfer", Guid.NewGuid());
        var inbound = balance.Adjust(Guid.NewGuid(), 1, "Physical return restock", Guid.NewGuid());
        var returned = new PhysicalReturn(order.Id, Guid.NewGuid(), new string('E', 64), "RETURN-ONE", "Item inspected", Now,
            false, Guid.NewGuid(), [new(shipment.Id, orderItem.Id, variant.Id, 1, 1, inbound.Id)]);
        db.AddRange(type, product, otherProduct, location, balance, opening, hold, outbound, shipment, receipt, refund, inbound, returned);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return (order, other, receipt, shipment, refund, returned, hold);
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task RefundsAndReturnsUseExactReceiptAndShipmentKeysAndDatabaseChecks(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var seeded = await Seed(db);
        var line = seeded.Return.Items.Single();
        await AssertConstraintAsync(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Payments"."Refunds" SET "OrderId" = {seeded.Other.Id} WHERE "Id" = {seeded.Refund.Id}
            """));
        await AssertConstraintAsync(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Payments"."Refunds" SET "Amount" = 0 WHERE "Id" = {seeded.Refund.Id}
            """));
        await AssertConstraintAsync(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Payments"."Refunds" SET "Amount" = 0.5 WHERE "Id" = {seeded.Refund.Id}
            """));
        await AssertConstraintAsync(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Payments"."Refunds" SET "Source" = 'Unknown' WHERE "Id" = {seeded.Refund.Id}
            """));
        await AssertConstraintAsync(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Shipping"."PhysicalReturnItems" SET "OrderId" = {seeded.Other.Id} WHERE "ReturnId" = {seeded.Return.Id}
            """));
        await AssertConstraintAsync(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Shipping"."PhysicalReturnItems" SET "OrderItemId" = {seeded.Other.Items.Single().Id} WHERE "ReturnId" = {seeded.Return.Id}
            """));
        await AssertConstraintAsync(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Shipping"."PhysicalReturnItems" SET "VariantId" = {seeded.Other.Items.Single().VariantId} WHERE "ReturnId" = {seeded.Return.Id}
            """));
        await AssertConstraintAsync(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Shipping"."PhysicalReturnItems" SET "StockMovementId" = NULL WHERE "ReturnId" = {seeded.Return.Id}
            """));
        await AssertConstraintAsync(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Shipping"."PhysicalReturnItems" SET "RestockQuantity" = 2 WHERE "ReturnId" = {seeded.Return.Id}
            """));
        db.Refunds.Add(new(seeded.Receipt.Id, seeded.Order.Id, seeded.Refund.OperationId, new string('F', 64), "REFUND-TWO",
            10, "VND", Now, Now, Now, "Other refund", "ManualCash", Guid.NewGuid()));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        db.Refunds.Add(new(seeded.Receipt.Id, seeded.Order.Id, Guid.NewGuid(), new string('F', 64), "refund-one",
            10, "VND", Now, Now, Now, "Other refund", "ManualBankTransfer", Guid.NewGuid()));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        db.PhysicalReturns.Add(new(seeded.Order.Id, Guid.NewGuid(), new string('F', 64), "RETURN-TWO", "Repeat restock", Now,
            false, Guid.NewGuid(), [new(line.ShipmentId, line.OrderItemId, line.VariantId, 1, 1, line.StockMovementId)]));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        db.PhysicalReturns.Add(new(seeded.Order.Id, seeded.Return.OperationId, new string('F', 64), "RETURN-THREE", "Repeated operation", Now,
            false, Guid.NewGuid(), [new(line.ShipmentId, line.OrderItemId, line.VariantId, 1, 0, null)]));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        db.PhysicalReturns.Add(new(seeded.Order.Id, Guid.NewGuid(), new string('F', 64), "return-one", "Repeated document", Now,
            false, Guid.NewGuid(), [new(line.ShipmentId, line.OrderItemId, line.VariantId, 1, 0, null)]));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        Assert.Single(await db.Refunds.AsNoTracking().ToListAsync());
        Assert.Single(await db.PhysicalReturnItems.AsNoTracking().ToListAsync());
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task AfterSalesHistoryIsAppendOnlyAuditedAndClosureChecksMatchDomain(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        var actor = Guid.NewGuid();
        using var scope = fixture.Scope(actor);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var seeded = await Seed(db);
        var refund = await db.Refunds.SingleAsync();
        db.Entry(refund).Property(x => x.Amount).CurrentValue = 100;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        db.Refunds.Remove(await db.Refunds.SingleAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        var returned = await db.PhysicalReturns.SingleAsync();
        db.Entry(returned).Property(x => x.Reason).CurrentValue = "Altered history";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        db.PhysicalReturns.Remove(await db.PhysicalReturns.SingleAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        var line = await db.PhysicalReturnItems.SingleAsync();
        db.Entry(line).Property(x => x.Quantity).CurrentValue = 2;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        db.PhysicalReturnItems.Remove(await db.PhysicalReturnItems.SingleAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        await AssertConstraintAsync(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Ordering"."Orders" SET "Status" = 6 WHERE "Id" = {seeded.Order.Id}
            """));
        await AssertConstraintAsync(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Ordering"."PhysicalStockReservations" SET "State" = 4, "ConsumedQuantity" = "Quantity" WHERE "Id" = {seeded.Hold.Id}
            """));
        var order = await db.Orders.SingleAsync(x => x.Id == seeded.Order.Id);
        order.CloseFulfillment();
        var hold = await db.PhysicalStockReservations.SingleAsync();
        Assert.Equal(1, hold.CloseRemainder());
        var balance = await db.InventoryItems.SingleAsync();
        balance.Release(1);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        Assert.Equal(OrderStatus.Closed, (await db.Orders.AsNoTracking().SingleAsync(x => x.Id == order.Id)).Status);
        Assert.Equal(HoldState.Closed, (await db.PhysicalStockReservations.AsNoTracking().SingleAsync()).State);
        var trail = await db.Trails.AsNoTracking().SingleAsync(x => x.TableName == "PhysicalReturnItems" && x.Type == Boilerate.Domain.Auditing.TrailType.Create);
        Assert.Equal(actor, trail.UserId);
        using var keys = JsonDocument.Parse(trail.PrimaryKey);
        Assert.Equal(seeded.Return.Id, keys.RootElement.GetProperty("returnId").GetGuid());
        Assert.Equal(seeded.Shipment.Id, keys.RootElement.GetProperty("shipmentId").GetGuid());
        Assert.Equal(seeded.Return.Items.Single().OrderItemId, keys.RootElement.GetProperty("orderItemId").GetGuid());
        using var values = JsonDocument.Parse(trail.NewValues!);
        Assert.Equal(1, values.RootElement.GetProperty("restockQuantity").GetInt32());
        Assert.Equal(seeded.Return.Items.Single().StockMovementId, values.RootElement.GetProperty("stockMovementId").GetGuid());
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task EmptyMigrationRoundtripsButClosedUnpaidCodBlocksDowngradeBeforeAnyDdl(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var migrator = db.GetService<IMigrator>();
        var previous = db.Database.GetMigrations().Single(x => x.EndsWith("_ManualRenewals", StringComparison.Ordinal));
        var before = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        Assert.EndsWith("_RefundsReturns", before.Last());
        await migrator.MigrateAsync(previous);
        Assert.Equal(previous, (await db.Database.GetAppliedMigrationsAsync()).Last());
        await migrator.MigrateAsync();
        Assert.Equal(before, (await db.Database.GetAppliedMigrationsAsync()).ToArray());
        Assert.False(db.Database.HasPendingModelChanges());
        var type = new ProductType("CLOSED-COD", "Closed COD");
        var product = new Product(type.Id, "COD product", "closed-cod");
        var variant = product.AddVariant("CLOSED-COD-SKU", 100, null, new Dictionary<Guid, Guid>(), 200);
        var cart = new Cart(Guid.NewGuid());
        var item = new OrderItem(product.Id, variant.Id, product.Name, variant.Sku, "", ProductFulfillmentKind.Physical, 100, 1);
        var order = new Order(cart.CustomerId, cart.Id, Guid.NewGuid(), new string('A', 64), OrderPaymentMethod.CashOnDelivery,
            "COD customer", "cod@example.test", "0901234567", "123 Test Street", 0, Now, 30, [item]);
        order.RegisterPhysicalShipment(false);
        order.CloseFulfillment();
        db.AddRange(type, product, cart, order);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var blocked = await Record.ExceptionAsync(() => migrator.MigrateAsync(previous));
        Assert.True(blocked is Microsoft.Data.SqlClient.SqlException { Number: 51000 } ||
            blocked is Npgsql.PostgresException { SqlState: "P0001" }, blocked?.ToString() ?? "Downgrade must retain closed order history.");
        Assert.Equal(before, (await db.Database.GetAppliedMigrationsAsync()).ToArray());
        Assert.Equal(OrderStatus.Closed, (await db.Orders.AsNoTracking().SingleAsync()).Status);
        Assert.Empty(await db.Refunds.AsNoTracking().ToListAsync());
        Assert.Empty(await db.PhysicalReturns.AsNoTracking().ToListAsync());
    }
}
