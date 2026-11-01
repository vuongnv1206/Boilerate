using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using Boilerate.Domain.Ordering;
using Xunit;

namespace Boilerate.Tests;

public sealed class DiscountTests
{
    private static readonly DateTime Now = new(2026, 10, 30, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void QuoteUsesWholeDongAndChecksTimeLimitAndPositiveMerchandise()
    {
        var code = new Discount("save10", "Giảm giá", DiscountKind.Percentage, 10, 100, Now, Now.AddDays(1), 1, true);
        Assert.Equal("SAVE10", code.Code);
        Assert.Equal((10m, (string?)null), code.Quote(109, Now, 0));
        Assert.NotNull(code.Quote(99, Now, 0).Error);
        Assert.NotNull(code.Quote(109, Now.AddTicks(-1), 0).Error);
        Assert.NotNull(code.Quote(109, Now.AddDays(1), 0).Error);
        Assert.NotNull(code.Quote(109, Now, 1).Error);
        Assert.Throws<DomainException>(() => new Discount("FREE", "Free", DiscountKind.Percentage, 100, 0, null, null, null, true));
        var fixedCode = new Discount("FULL", "Fixed", DiscountKind.Fixed, 109, 0, null, null, null, true);
        Assert.NotNull(fixedCode.Quote(109, Now, 0).Error);
        Assert.Throws<DomainException>(() => new Discount("A B", "Invalid", DiscountKind.Fixed, 1, 0, null, null, null, true));
    }

    [Fact]
    public void OrderSplitsRoundingDeterministicallyAndPreservesSnapshot()
    {
        var a = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var b = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var items = new[]
        {
            new OrderItem(Guid.NewGuid(), b, "B", "B", "", ProductFulfillmentKind.Physical, 1, 1),
            new OrderItem(Guid.NewGuid(), a, "A", "A", "", ProductFulfillmentKind.Physical, 1, 1),
        };
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new string('a', 64),
            OrderPaymentMethod.Prepaid, "Customer", "customer@example.test", "123", "Address", 3, Now, 30, items);
        var discount = new Discount("HALF", "Half", DiscountKind.Fixed, 1, 0, null, null, null, true);
        order.ApplyDiscount(discount, 1);
        Assert.Equal(4, order.Total);
        Assert.Equal(1, order.Items.Single(i => i.VariantId == a).DiscountAmount);
        Assert.Equal(0, order.Items.Single(i => i.VariantId == b).DiscountAmount);
        Assert.Equal(order.DiscountAmount, order.Items.Sum(i => i.DiscountAmount));
        discount.Update("CHANGED", "Changed", DiscountKind.Fixed, 1, 0, null, null, null, true);
        Assert.Equal("HALF", order.DiscountCode);
        Assert.Equal(1, order.DiscountAmount);
        Assert.Throws<DomainException>(() => order.ApplyDiscount(discount, 1));
    }
}
