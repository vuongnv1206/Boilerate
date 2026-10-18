using System.Text.Json;
using Boilerate.Application.Ordering;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Ordering;
using Boilerate.Domain.Payments;
using Boilerate.Infrastructure.Persistence.Context;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Boilerate.Tests;

[Collection("Foundation")]
public sealed class PaymentTimestampTests
{
    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task OrderDetailAndSummaryReturnUtcTimestampsWithTheSameInstantsAndJsonTimezone(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        var customer = Guid.NewGuid();
        using var scope = fixture.Scope(customer);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var type = new ProductType("TIMESTAMP", "Timestamp test");
        var product = new Product(type.Id, "Manual service", "timestamp-service", fulfillmentKind: ProductFulfillmentKind.ManualExtension);
        var variant = product.AddVariant("TIMESTAMP-SKU", 100, null, new Dictionary<Guid, Guid>(), 200);
        var cart = new Cart(customer);
        var checkoutAt = new DateTime(2026, 10, 18, 9, 0, 0, 123, DateTimeKind.Utc);
        var paidAt = checkoutAt.AddMinutes(1);
        var order = new Order(customer, cart.Id, Guid.NewGuid(), new string('A', 64), OrderPaymentMethod.Prepaid,
            "Timestamp customer", "customer@example.test", "0901234567", null, 0, checkoutAt, 30,
            [new OrderItem(product.Id, variant.Id, product.Name, variant.Sku, "", product.FulfillmentKind, 100, 1)]);
        order.RecordPayment(paidAt);
        var receipt = new PaymentReceipt(order.Id, Guid.NewGuid(), new string('B', 64), "BANK-UTC-1", order.Total, "VND", paidAt, paidAt);
        db.AddRange(type, product, cart, order, receipt);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var ordering = scope.ServiceProvider.GetRequiredService<IOrderingService>();
        var detail = await ordering.GetOrderAsync(order.Id, false, default);
        var summary = Assert.Single((await ordering.OrdersAsync(1, 20, false, default)).Items);
        var expiry = checkoutAt.AddMinutes(30);
        Assert.Equal(paidAt, detail.PaidAtUtc);
        Assert.Equal(expiry, detail.ExpiresAt);
        Assert.Equal(DateTimeKind.Utc, detail.PaidAtUtc!.Value.Kind);
        Assert.Equal(DateTimeKind.Utc, detail.ExpiresAt!.Value.Kind);
        Assert.Equal(paidAt, summary.PaidAtUtc);
        Assert.Equal(expiry, summary.ExpiresAt);
        Assert.Equal(DateTimeKind.Utc, summary.PaidAtUtc!.Value.Kind);
        Assert.Equal(DateTimeKind.Utc, summary.ExpiresAt!.Value.Kind);
        Assert.Equal(DateTimeKind.Utc, summary.CreatedOn.Kind);

        var detailJson = JsonSerializer.SerializeToElement(detail);
        var summaryJson = JsonSerializer.SerializeToElement(summary);
        foreach (var json in new[] { detailJson, summaryJson })
        {
            Assert.EndsWith("Z", json.GetProperty(nameof(OrderDto.PaidAtUtc)).GetString());
            Assert.EndsWith("Z", json.GetProperty(nameof(OrderDto.ExpiresAt)).GetString());
        }
        Assert.EndsWith("Z", summaryJson.GetProperty(nameof(OrderSummary.CreatedOn)).GetString());
    }
}
