using Boilerate.Domain.Catalog;
using Boilerate.Domain.DigitalInventory;
using Boilerate.Domain.Inventory;
using Boilerate.Domain.Ordering;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Infrastructure.Persistence.Configuration;

internal static class OrderingModel
{
    internal static void Configure(ModelBuilder m, bool postgres)
    {
        foreach (var type in new[] { typeof(Cart), typeof(Order), typeof(PhysicalStockReservation) })
        {
            var entity = m.Entity(type);
            entity.HasBaseType((Type?)null);
            entity.HasKey("Id");
            entity.Property<Guid>("Id").ValueGeneratedNever();
            entity.Property("Version").HasMaxLength(32).IsConcurrencyToken();
        }

        var cart = m.Entity<Cart>();
        cart.ToTable("Carts", "Ordering");
        cart.HasIndex(x => x.CustomerId).IsUnique();

        var cartItem = m.Entity<CartItem>();
        cartItem.ToTable("CartItems", "Ordering", t =>
            t.HasCheckConstraint("CK_CartItem_Quantity", "\"Quantity\" BETWEEN 1 AND 999"));
        cartItem.HasKey(x => new { x.CartId, x.VariantId });
        cartItem.HasOne<Cart>().WithMany(x => x.Items).HasForeignKey(x => x.CartId).OnDelete(DeleteBehavior.Restrict);
        cartItem.HasOne<ProductVariant>().WithMany().HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.Restrict);

        var order = m.Entity<Order>();
        order.ToTable("Orders", "Ordering", t =>
        {
            t.HasCheckConstraint("CK_Order_Status", "\"Status\" IN (1,2,3,4,5,6)");
            t.HasCheckConstraint("CK_Order_PaymentStatus", "\"PaymentStatus\" IN (1,2)");
            t.HasCheckConstraint("CK_Order_FulfillmentStatus", "\"FulfillmentStatus\" IN (1,2,3,4,5)");
            t.HasCheckConstraint("CK_Order_Closure", "(\"Status\" = 6 AND \"FulfillmentStatus\" = 5 AND (\"PaymentStatus\" = 2 OR \"PaymentMethod\" = 2)) OR (\"Status\" <> 6 AND \"FulfillmentStatus\" <> 5)");
            t.HasCheckConstraint("CK_Order_PaidAt", "(\"PaymentStatus\" = 1 AND \"PaidAtUtc\" IS NULL) OR (\"PaymentStatus\" = 2 AND \"PaidAtUtc\" IS NOT NULL)");
            t.HasCheckConstraint("CK_Order_Completion", "\"Status\" <> 5 OR (\"PaymentStatus\" = 2 AND \"FulfillmentStatus\" = 4)");
            t.HasCheckConstraint("CK_Order_PaymentMethod", "\"PaymentMethod\" IN (1,2)");
            t.HasCheckConstraint("CK_Order_Expiry", "(\"Status\" <> 1 OR \"ExpiresAt\" IS NOT NULL) AND (\"PaymentMethod\" <> 2 OR \"ExpiresAt\" IS NULL)");
            t.HasCheckConstraint("CK_Order_Amounts", "\"Currency\" = 'VND' AND \"Subtotal\" >= 0 AND \"Subtotal\" = FLOOR(\"Subtotal\") AND \"ShippingAmount\" >= 0 AND \"ShippingAmount\" = FLOOR(\"ShippingAmount\") AND \"Total\" >= 0 AND \"Total\" = FLOOR(\"Total\") AND \"Total\" = \"Subtotal\" + \"ShippingAmount\"");
        });
        order.Property(x => x.Number).HasMaxLength(40).IsUnicode(false).IsRequired();
        order.Property(x => x.RequestHash).HasMaxLength(64).IsUnicode(false).IsRequired();
        order.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        order.Property(x => x.Subtotal).HasPrecision(18, 2);
        order.Property(x => x.ShippingAmount).HasPrecision(18, 2);
        order.Property(x => x.Total).HasPrecision(18, 2);
        order.Property(x => x.PaymentStatus).HasDefaultValue(OrderPaymentStatus.Unpaid);
        order.Property(x => x.FulfillmentStatus).HasDefaultValue(OrderFulfillmentStatus.Pending);
        order.Property(x => x.PhysicalDispatchStarted).HasDefaultValue(false);
        order.Property(x => x.RecipientName).HasMaxLength(256).IsRequired();
        order.Property(x => x.Email).HasMaxLength(256).IsRequired();
        order.Property(x => x.Phone).HasMaxLength(30).IsRequired();
        order.Property(x => x.ShippingAddress).HasMaxLength(1000);
        order.HasIndex(x => x.Number).IsUnique();
        order.HasIndex(x => new { x.CustomerId, x.OperationId }).IsUnique();
        order.HasIndex(x => new { x.CustomerId, x.CreatedOn, x.Id });
        order.HasIndex(x => new { x.Status, x.ExpiresAt });
        order.HasOne<Cart>().WithMany().HasForeignKey(x => x.CartId).OnDelete(DeleteBehavior.Restrict);

        var orderItem = m.Entity<OrderItem>();
        orderItem.HasBaseType((Type?)null);
        orderItem.ToTable("OrderItems", "Ordering", t =>
        {
            t.HasCheckConstraint("CK_OrderItem_Quantity", "\"Quantity\" BETWEEN 1 AND 999");
            t.HasCheckConstraint("CK_OrderItem_Price", "\"UnitPrice\" >= 0 AND \"UnitPrice\" = FLOOR(\"UnitPrice\")");
            t.HasCheckConstraint("CK_OrderItem_FulfillmentKind", "\"FulfillmentKind\" IN (1,2,3,4)");
        });
        orderItem.HasKey(x => x.Id);
        orderItem.Property(x => x.Id).ValueGeneratedNever();
        orderItem.HasAlternateKey(x => new { x.Id, x.VariantId });
        orderItem.HasIndex(x => new { x.OrderId, x.VariantId }).IsUnique();
        orderItem.Property(x => x.Name).HasMaxLength(256).IsRequired();
        orderItem.Property(x => x.Sku).HasMaxLength(100).IsRequired();
        orderItem.Property(x => x.OptionDescription).HasMaxLength(2000).IsRequired();
        orderItem.Property(x => x.UnitPrice).HasPrecision(18, 2);
        orderItem.HasOne<Order>().WithMany(x => x.Items).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        orderItem.HasOne<ProductVariant>().WithMany().HasForeignKey(x => new { x.ProductId, x.VariantId })
            .HasPrincipalKey(x => new { x.ProductId, x.Id }).OnDelete(DeleteBehavior.Restrict);

        var reservation = m.Entity<PhysicalStockReservation>();
        reservation.ToTable("PhysicalStockReservations", "Ordering", t =>
        {
            t.HasCheckConstraint("CK_PhysicalStockReservation_Quantity", "\"Quantity\" > 0");
            t.HasCheckConstraint("CK_PhysicalStockReservation_State", "(\"State\" IN (1,4) AND \"ConsumedQuantity\" < \"Quantity\") OR (\"State\" = 2 AND \"ConsumedQuantity\" = 0) OR (\"State\" = 3 AND \"ConsumedQuantity\" = \"Quantity\")");
            t.HasCheckConstraint("CK_PhysicalStockReservation_Consumed", "\"ConsumedQuantity\" >= 0 AND \"ConsumedQuantity\" <= \"Quantity\"");
        });
        reservation.Property(x => x.ConsumedQuantity).HasDefaultValue(0);
        reservation.HasIndex(x => x.OrderItemId).IsUnique();
        reservation.HasOne<OrderItem>().WithMany().HasForeignKey(x => new { x.OrderItemId, x.VariantId })
            .HasPrincipalKey(x => new { x.Id, x.VariantId }).OnDelete(DeleteBehavior.Restrict);
        reservation.HasOne<InventoryItem>().WithMany().HasForeignKey(x => new { x.InventoryItemId, x.VariantId })
            .HasPrincipalKey(x => new { x.Id, x.VariantId }).OnDelete(DeleteBehavior.Restrict);

        m.Entity<DigitalStockItem>().HasOne<OrderItem>().WithMany()
            .HasForeignKey(x => new { x.ReservedForOrderItemId, x.VariantId })
            .HasPrincipalKey(x => new { x.Id, x.VariantId }).OnDelete(DeleteBehavior.Restrict);
    }
}
