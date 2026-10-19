using System.Text.Json;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Ordering;
using Boilerate.Domain.Renewals;
using Boilerate.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Boilerate.Tests;

[Collection("Foundation")]
public sealed class RenewalMigrationTests
{
    private static readonly DateTime Now = new(2026, 10, 18, 15, 0, 0, DateTimeKind.Utc);

    private static async Task<(RenewalRequest First, RenewalRequest Second, Order Other)> Seed(ApplicationDbContext db)
    {
        var type = new ProductType("RENEWAL-MIGRATION", "Renewal migration");
        var product = new Product(type.Id, "Account extension", "renewal-migration", fulfillmentKind: ProductFulfillmentKind.ManualExtension);
        var variant = product.AddVariant("RENEWAL-SKU", 100, null, new Dictionary<Guid, Guid>(), 200);
        Order CreateOrder()
        {
            var cart = new Cart(Guid.NewGuid());
            var item = new OrderItem(product.Id, variant.Id, product.Name, variant.Sku, "30 days", ProductFulfillmentKind.ManualExtension, 100, 2);
            var order = new Order(cart.CustomerId, cart.Id, Guid.NewGuid(), new string('A', 64), OrderPaymentMethod.Prepaid,
                "Renewal customer", "renewal@example.test", "0901234567", null, 0, Now, 30, [item]);
            order.RecordPayment(Now);
            db.AddRange(cart, order);
            return order;
        }
        var firstOrder = CreateOrder();
        var secondOrder = CreateOrder();
        var otherOrder = CreateOrder();
        var first = new RenewalRequest(firstOrder.Id, firstOrder.Items.Single().Id, variant.Id);
        var second = new RenewalRequest(secondOrder.Id, secondOrder.Items.Single().Id, variant.Id);
        first.SetTarget("protected-target-secret-one", new string('B', 64), "a***1");
        second.SetTarget("protected-target-secret-two", new string('B', 64), "a***1");
        db.AddRange(type, product, first, second);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return (first, second, otherOrder);
    }

    private static async Task AssertConstraintAsync(Func<Task> action)
    {
        var exception = await Record.ExceptionAsync(action);
        Assert.True(exception is Microsoft.Data.SqlClient.SqlException { Number: 547 or 2601 or 2627 } ||
            exception is Npgsql.PostgresException { SqlState: "23503" or "23505" or "23514" },
            exception?.ToString() ?? "Expected a database FK, unique or check constraint violation.");
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task RequestsAreBoundToTheirExactOrderLineAndOnlyOneActiveClaimUsesATarget(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var seeded = await Seed(db);
        await AssertConstraintAsync(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Renewals"."RenewalRequests" SET "OrderId" = {seeded.Other.Id} WHERE "Id" = {seeded.First.Id}
            """));
        await AssertConstraintAsync(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Renewals"."RenewalRequests" SET "FulfillmentKind" = 1 WHERE "Id" = {seeded.First.Id}
            """));
        await AssertConstraintAsync(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Renewals"."RenewalRequests" SET "Status" = 4 WHERE "Id" = {seeded.First.Id}
            """));
        db.RenewalRequests.Add(new(seeded.First.OrderId, seeded.First.OrderItemId, seeded.First.VariantId));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        var first = await db.RenewalRequests.SingleAsync(x => x.Id == seeded.First.Id);
        first.Claim(Guid.NewGuid(), Now, 15);
        await db.SaveChangesAsync();
        var second = await db.RenewalRequests.SingleAsync(x => x.Id == seeded.Second.Id);
        second.Claim(Guid.NewGuid(), Now.AddMinutes(16), 15);
        // Expiry alone cannot make a filtered unique key disappear. The service must release it explicitly.
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        first = await db.RenewalRequests.SingleAsync(x => x.Id == seeded.First.Id);
        Assert.True(first.ReleaseExpired(Now.AddMinutes(16)));
        await db.SaveChangesAsync();
        second = await db.RenewalRequests.SingleAsync(x => x.Id == seeded.Second.Id);
        second.Claim(Guid.NewGuid(), Now.AddMinutes(17), 15);
        await db.SaveChangesAsync();
        Assert.Equal(1, await db.RenewalRequests.CountAsync(x => x.Status == RenewalStatus.Claimed));
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task AttemptAndOperationHistoryIsImmutableAndSecretsStayOutOfAudit(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var seeded = await Seed(db);
        var request = await db.RenewalRequests.Include(x => x.Attempts).SingleAsync(x => x.Id == seeded.First.Id);
        var actor = Guid.NewGuid();
        var token = request.Claim(actor, Now, 15);
        var operationId = Guid.NewGuid();
        var attempt = request.Start(actor, token, operationId, Now.AddMinutes(1));
        var operation = new RenewalOperation(request.Id, operationId, actor, "Start", new string('C', 64), request.Status, Now.AddMinutes(1));
        db.RenewalOperations.Add(operation);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        db.RenewalAttempts.Add(new(request.Id, Guid.NewGuid(), actor, Now.AddMinutes(2)));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        var persistedAttempt = await db.RenewalAttempts.SingleAsync();
        db.Entry(persistedAttempt).Property(x => x.StartedBy).CurrentValue = Guid.NewGuid();
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        request = await db.RenewalRequests.Include(x => x.Attempts).SingleAsync(x => x.Id == seeded.First.Id);
        request.Verify(attempt.Id, true, Now.AddDays(30), Guid.NewGuid(), Now.AddMinutes(3), "protected-evidence-secret");
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        await AssertConstraintAsync(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Renewals"."RenewalAttempts" SET "NewExpiresAtUtc" = "VerifiedAtUtc" WHERE "Id" = {attempt.Id}
            """));
        persistedAttempt = await db.RenewalAttempts.SingleAsync();
        db.Entry(persistedAttempt).Property(x => x.ProtectedEvidence).CurrentValue = "changed-protected-evidence";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        db.RenewalAttempts.Remove(await db.RenewalAttempts.SingleAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        db.RenewalRequests.Remove(await db.RenewalRequests.SingleAsync(x => x.Id == seeded.First.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        var persistedOperation = await db.RenewalOperations.SingleAsync();
        db.Entry(persistedOperation).Property(x => x.Action).CurrentValue = "Changed";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        db.RenewalOperations.Remove(await db.RenewalOperations.SingleAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        db.RenewalOperations.Add(new(seeded.Second.Id, operationId, actor, "Claim", new string('D', 64), RenewalStatus.Ready, Now));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        var audit = JsonSerializer.Serialize(await db.Trails.AsNoTracking().ToListAsync());
        Assert.DoesNotContain("ProtectedTarget", audit);
        Assert.DoesNotContain("TargetKey", audit);
        Assert.DoesNotContain("ClaimToken", audit);
        Assert.DoesNotContain("ProtectedEvidence", audit);
        Assert.DoesNotContain("protected-target-secret", audit);
        Assert.DoesNotContain("protected-evidence-secret", audit);
        Assert.Contains("RenewalAttempts", audit);
        Assert.Contains(attempt.Id.ToString(), audit);
        Assert.Contains("VerifiedBy", audit);
        Assert.Single(await db.RenewalAttempts.AsNoTracking().ToListAsync());
        Assert.Single(await db.RenewalOperations.AsNoTracking().ToListAsync());
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task EmptyMigrationRoundtripsAndRenewalHistoryBlocksDowngradeBeforeAnyDdl(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var migrator = db.GetService<IMigrator>();
        var previous = db.Database.GetMigrations().Single(x => x.EndsWith("_PhysicalShipping", StringComparison.Ordinal));
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
            blocked is Npgsql.PostgresException { SqlState: "P0001" }, blocked?.ToString() ?? "Downgrade must retain renewal history.");
        Assert.Equal(before, (await db.Database.GetAppliedMigrationsAsync()).ToArray());
        Assert.Equal(2, await db.RenewalRequests.CountAsync());
        Assert.Equal(seeded.First.ProtectedTarget, (await db.RenewalRequests.AsNoTracking().SingleAsync(x => x.Id == seeded.First.Id)).ProtectedTarget);
    }
}
