using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Persistence;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using Boilerate.Domain.Ordering;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Ordering.Shared;

internal static class CheckoutExecutor
{
    private static string RequestHash(Checkout r) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            r.CartVersion,
            r.PaymentMethod,
            RecipientName = r.RecipientName?.Trim(),
            Email = r.Email?.Trim(),
            Phone = r.Phone?.Trim(),
            ShippingAddress = string.IsNullOrWhiteSpace(r.ShippingAddress) ? null : r.ShippingAddress.Trim(),
            ExpectedTotal = r.ExpectedTotal.ToString("0.##", CultureInfo.InvariantCulture),
        }))));

    internal static async Task<OrderDto> CheckoutForAsync(
        IApplicationDbContext db,
        ICommerceUnitOfWork work,
        OrderingSettings settings,
        DateTime nowUtc,
        Guid customer,
        string? verifiedGuestEmail,
        Checkout r,
        CancellationToken ct)
    {
        if (r.OperationId == Guid.Empty || string.IsNullOrWhiteSpace(r.CartVersion))
        {
            throw new DomainException("OperationId and CartVersion are required.");
        }

        CatalogRules.Money(r.ExpectedTotal, null);
        string hash = RequestHash(r);
        await OrderStockCoordinator.ExpirePendingOrdersAsync(db, work, settings, nowUtc, ct);
        return await work.ExecuteAsync(async () =>
        {
            await OrderingAuthHelper.AcquireOrderingGateAsync(db, ct);
            var prior = await OrderStockCoordinator.Orders(db)
                .SingleOrDefaultAsync(o => o.CustomerId == customer && o.OperationId == r.OperationId, ct);
            if (prior is not null)
            {
                if (prior.RequestHash != hash)
                {
                    throw new ConflictException("OperationId was already used with different checkout content.");
                }

                return OrderFulfillmentCoordinator.MapOrder(prior);
            }

            var cart = await CartQuoteBuilder.Carts(db).SingleOrDefaultAsync(c => c.CustomerId == customer, ct)
                ?? throw new ConflictException("Cart is empty.");
            CommerceConcurrency.CheckVersion(r.CartVersion, cart.Version);
            if (cart.Items.Count == 0)
            {
                throw new ConflictException("Cart is empty.");
            }

            var variantIds = cart.Items.Select(i => i.VariantId).ToArray();
            var productIds = await db.ProductVariants
                .Where(v => variantIds.Contains(v.Id))
                .Select(v => v.ProductId)
                .Distinct()
                .ToArrayAsync(ct);
            var products = await OrderStockCoordinator.LockProductsAsync(db, productIds, ct);
            var items = new List<OrderItem>();
            foreach (var line in cart.Items.OrderBy(i => i.VariantId))
            {
                var product = products.Values.Single(p => p.Variants.Any(v => v.Id == line.VariantId));
                var variant = product.GetVariant(line.VariantId);
                if (product.IsDeleted || variant.IsRetired || product.Status != ProductStatus.Published || !variant.IsActive || !variant.Price.HasValue)
                {
                    throw new ConflictException("A cart product is no longer available for purchase.");
                }

                var selections = variant.Selections.OrderBy(s => s.OptionId).Select(s =>
                {
                    var option = product.Options.Single(o => o.Id == s.OptionId);
                    return option.Name + ": " + option.Values.Single(v => v.Id == s.OptionValueId).Label;
                });
                items.Add(new OrderItem(
                    product.Id,
                    variant.Id,
                    product.Name,
                    variant.Sku,
                    string.Join("; ", selections),
                    product.FulfillmentKind,
                    variant.Price.Value,
                    line.Quantity,
                    variant.TrackInventory));
            }

            decimal shipping = items.Any(i => i.FulfillmentKind == ProductFulfillmentKind.Physical) ? settings.FlatShippingAmount : 0;
            CatalogRules.Money(shipping, null);
            if (verifiedGuestEmail is not null && r.PaymentMethod == OrderPaymentMethod.Prepaid)
            {
                var payment = await db.Set<StorefrontPaymentSettings>()
                    .AsNoTracking()
                    .SingleOrDefaultAsync(s => s.Id == StorefrontPaymentSettings.SingletonId, ct);
                if (payment is null ||
                    !payment.BankTransferEnabled ||
                    string.IsNullOrWhiteSpace(payment.BankName) ||
                    string.IsNullOrWhiteSpace(payment.AccountNumber) ||
                    string.IsNullOrWhiteSpace(payment.AccountHolder))
                {
                    throw new ConflictException("Bank transfer is not configured for checkout.");
                }
            }

            var order = new Order(
                customer,
                cart.Id,
                r.OperationId,
                hash,
                r.PaymentMethod,
                r.RecipientName,
                r.Email,
                r.Phone,
                r.ShippingAddress,
                shipping,
                nowUtc,
                settings.ReservationMinutes,
                items,
                verifiedGuestEmail is not null);
            var discount = await CartQuoteBuilder.DiscountQuoteAsync(db, nowUtc, cart.DiscountCode, order.Subtotal, ct);
            if (discount.Error is not null)
            {
                throw new ConflictException(discount.Error);
            }

            if (discount.Discount is not null)
            {
                order.ApplyDiscount(discount.Discount, discount.Amount);
            }

            if (order.Total != r.ExpectedTotal)
            {
                throw new ConflictException("Prices or shipping changed; confirm the current total before checkout.");
            }

            db.Orders.Add(order);
            await OrderStockCoordinator.ReserveStockAsync(db, order, ct);
            db.Set<CartItem>().RemoveRange(cart.Items.ToArray());
            cart.ReplaceItems([]);
            cart.SetDiscountCode(null);
            return OrderFulfillmentCoordinator.MapOrder(order);
        }, ct);
    }
}

