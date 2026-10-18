using Boilerate.Application.Catalog;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Identity.Users;
using Boilerate.Application.Ordering;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.DigitalInventory;
using Boilerate.Domain.Ordering;
using Boilerate.Infrastructure.Commerce;
using Boilerate.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Boilerate.Tests;

[Collection("Foundation")]
public sealed class OrderingMigrationTests
{
    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task DowngradeRefusesActiveHoldsAndReleasedDowngradeUpgradePreservesExistingStock(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var actor = scope.ServiceProvider.GetRequiredService<ICurrentUser>();
        var users = Substitute.For<IUserService>();
        users.HasPermissionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        var work = new CommerceUnitOfWork(db);
        var catalog = new CommerceService(db, work, actor, users, Options.Create(new CommerceSettings()),
            scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>());
        var ordering = new OrderingService(db, work, actor, users, Options.Create(new OrderingSettings()), TimeProvider.System);
        await catalog.InitializeAsync(default);
        var type = await catalog.CreateTypeAsync(new("DOWNGRADE", "Downgrade fixture", []), default);
        async Task<ProductDto> CreatePublished(string slug, ProductFulfillmentKind kind)
        {
            var product = await catalog.CreateAsync(new(type.Id, type.Version, new("Product " + slug, slug, null, [], [], []),
                [], [new(slug, 100, null, new())], kind), default);
            return await catalog.StatusAsync(new(product.Id, product.Version, ProductStatus.Published), default);
        }
        var physical = await CreatePublished("preserved-shirt", ProductFulfillmentKind.Physical);
        var digital = await CreatePublished("preserved-account", ProductFulfillmentKind.DigitalUnique);
        var physicalId = physical.Variants.Single().Id;
        var digitalId = digital.Variants.Single().Id;
        var inventory = Assert.Single((await catalog.InventoryAsync(1, 20, default)).Items);
        await catalog.AdjustAsync(new(inventory.Id, inventory.Version, Guid.NewGuid(), 1, "Preserved receipt"), default);
        const string secret = "PreserveAccount@example.test\nPassword: KeepAcrossDowngrade!18";
        var stock = Assert.Single(await catalog.ImportDigitalAsync(new(digital.Id, digitalId, digital.Version, [new("ACCOUNT-ONE", secret)]), default));
        var protectedContent = (await db.DigitalStockItems.AsNoTracking().SingleAsync()).ProtectedContent;
        Assert.DoesNotContain(secret, protectedContent);
        var cart = await ordering.SaveCartAsync(new(null, [new(physicalId, 1), new(digitalId, 1)]), default);
        var order = await ordering.CheckoutAsync(new(cart.Version!, Guid.NewGuid(), OrderPaymentMethod.Prepaid,
            "Migration Customer", "migration@example.test", "0901234567", "123 Test Street", 200), default);
        var migrator = db.GetService<IMigrator>();
        var previous = db.Database.GetMigrations().Single(name => name.EndsWith("_DigitalInventory", StringComparison.Ordinal));
        var appliedBefore = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        Assert.EndsWith("_OrderingCheckout", appliedBefore.Last());

        var blocked = await Record.ExceptionAsync(() => migrator.MigrateAsync(previous));
        Assert.True(blocked is Microsoft.Data.SqlClient.SqlException { Number: 51000 } ||
            blocked is Npgsql.PostgresException { SqlState: "P0001" }, blocked?.ToString() ?? "Downgrade must reject active stock holds.");
        Assert.Equal(appliedBefore, (await db.Database.GetAppliedMigrationsAsync()).ToArray());
        Assert.Equal(1, (await db.InventoryItems.AsNoTracking().SingleAsync()).Reserved);
        Assert.Equal(1, (await db.InventoryItems.AsNoTracking().SingleAsync()).OnHand);
        var heldDigital = await db.DigitalStockItems.AsNoTracking().SingleAsync();
        Assert.Equal(DigitalStockState.Reserved, heldDigital.State);
        Assert.Equal(order.Items.Single(i => i.VariantId == digitalId).Id, heldDigital.ReservedForOrderItemId);
        Assert.Equal(protectedContent, heldDigital.ProtectedContent);
        Assert.Equal(OrderStatus.PendingPayment, (await ordering.GetOrderAsync(order.Id, false, default)).Status);
        Assert.Equal(HoldState.Held, (await db.PhysicalStockReservations.AsNoTracking().SingleAsync()).State);

        await ordering.CancelAsync(order.Id, new(order.Version), false, default);
        db.ChangeTracker.Clear();
        await migrator.MigrateAsync(previous);
        var appliedDown = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        Assert.Equal(previous, appliedDown.Last());
        Assert.DoesNotContain(appliedDown, name => name.EndsWith("_OrderingCheckout", StringComparison.Ordinal));
        // A deliberate downgrade removes the ordering tables and their history. Reapply before using the current EF model.
        await migrator.MigrateAsync();
        db.ChangeTracker.Clear();
        Assert.Equal(appliedBefore, (await db.Database.GetAppliedMigrationsAsync()).ToArray());
        Assert.Empty(await db.Orders.AsNoTracking().ToListAsync());
        Assert.Empty(await db.Carts.AsNoTracking().ToListAsync());
        Assert.Empty(await db.PhysicalStockReservations.AsNoTracking().ToListAsync());
        var balance = await db.InventoryItems.AsNoTracking().SingleAsync();
        Assert.Equal(inventory.Id, balance.Id);
        Assert.Equal(physicalId, balance.VariantId);
        Assert.Equal(1, balance.OnHand);
        Assert.Equal(0, balance.Reserved);
        var preservedDigital = await db.DigitalStockItems.AsNoTracking().SingleAsync();
        Assert.Equal(stock.Id, preservedDigital.Id);
        Assert.Equal(digitalId, preservedDigital.VariantId);
        Assert.Equal(DigitalStockState.Available, preservedDigital.State);
        Assert.Null(preservedDigital.ReservedForOrderItemId);
        Assert.Equal(protectedContent, preservedDigital.ProtectedContent);
        var revealed = await catalog.RevealDigitalStockAsync(stock.Id, default);
        Assert.Equal(stock.Id, revealed.Id);
        Assert.Equal(secret, revealed.Content);
        Assert.Equal(ProductFulfillmentKind.Physical, (await catalog.GetAsync(physical.Id, false, default)).FulfillmentKind);
        Assert.Equal(ProductFulfillmentKind.DigitalUnique, (await catalog.GetAsync(digital.Id, false, default)).FulfillmentKind);
        Assert.Equal(2, await db.Products.CountAsync());
        Assert.Single(await db.StockMovements.AsNoTracking().ToListAsync());
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
