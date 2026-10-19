using Boilerate.Domain.Inventory;
using Boilerate.Domain.Ordering;
using Boilerate.Domain.Shipping;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Infrastructure.Persistence.Configuration;

internal static class ShippingModel
{
    internal static void Configure(ModelBuilder m, bool postgres)
    {
        var shipment = m.Entity<Shipment>();
        shipment.HasBaseType((Type?)null);
        shipment.HasKey(x => x.Id);
        shipment.Property(x => x.Id).ValueGeneratedNever();
        shipment.Property(x => x.Version).HasMaxLength(32).IsConcurrencyToken();
        shipment.ToTable("Shipments", "Shipping", t =>
        {
            t.HasCheckConstraint("CK_Shipment_Status", "\"Status\" IN (1,2)");
            t.HasCheckConstraint("CK_Shipment_Delivery", "(\"Status\" = 1 AND \"DeliveredAtUtc\" IS NULL AND \"DeliveryOperationId\" IS NULL) OR (\"Status\" = 2 AND \"DeliveredAtUtc\" IS NOT NULL AND \"DeliveredAtUtc\" >= \"DispatchedAtUtc\" AND \"DeliveryOperationId\" IS NOT NULL)");
        });
        shipment.HasAlternateKey(x => new { x.Id, x.OrderId });
        shipment.Property(x => x.RequestHash).HasMaxLength(64).IsUnicode(false).IsRequired();
        shipment.Property(x => x.Carrier).HasMaxLength(100).IsRequired();
        shipment.Property(x => x.TrackingNumber).HasMaxLength(100).IsRequired();
        shipment.HasIndex(x => x.OperationId).IsUnique();
        shipment.HasIndex(x => x.DeliveryOperationId).IsUnique()
            .HasFilter(postgres ? "\"DeliveryOperationId\" IS NOT NULL" : "[DeliveryOperationId] IS NOT NULL");
        shipment.HasIndex(x => new { x.OrderId, x.DispatchedAtUtc, x.Id });
        shipment.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);

        var item = m.Entity<ShipmentItem>();
        item.ToTable("ShipmentItems", "Shipping", t =>
        {
            t.HasCheckConstraint("CK_ShipmentItem_Quantity", "\"Quantity\" BETWEEN 1 AND 999");
            t.HasCheckConstraint("CK_ShipmentItem_FulfillmentKind", "\"FulfillmentKind\" = 1");
        });
        item.HasKey(x => new { x.ShipmentId, x.OrderItemId });
        item.HasAlternateKey(x => new { x.ShipmentId, x.OrderItemId, x.OrderId, x.VariantId });
        item.HasOne<Shipment>().WithMany(x => x.Items)
            .HasForeignKey(x => new { x.ShipmentId, x.OrderId })
            .HasPrincipalKey(x => new { x.Id, x.OrderId }).OnDelete(DeleteBehavior.Restrict);
        item.HasOne<OrderItem>().WithMany()
            .HasForeignKey(x => new { x.OrderItemId, x.OrderId, x.VariantId, x.FulfillmentKind })
            .HasPrincipalKey(x => new { x.Id, x.OrderId, x.VariantId, x.FulfillmentKind })
            .OnDelete(DeleteBehavior.Restrict);
        item.HasIndex(x => x.StockMovementId).IsUnique();
        item.HasOne<StockMovement>().WithMany().HasForeignKey(x => x.StockMovementId).OnDelete(DeleteBehavior.Restrict);
    }
}
