using Boilerate.Domain.DigitalInventory;
using Boilerate.Domain.Ordering;
using Boilerate.Domain.Payments;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Infrastructure.Persistence.Configuration;

internal static class PaymentModel
{
    internal static void Configure(ModelBuilder m, bool postgres)
    {
        foreach (var type in new[] { typeof(PaymentReceipt), typeof(DigitalDelivery) })
        {
            var entity = m.Entity(type);
            entity.HasBaseType((Type?)null);
            entity.HasKey("Id");
            entity.Property<Guid>("Id").ValueGeneratedNever();
            entity.Property("Version").HasMaxLength(32).IsConcurrencyToken();
        }

        var receipt = m.Entity<PaymentReceipt>();
        receipt.ToTable("PaymentReceipts", "Payments", t =>
        {
            t.HasCheckConstraint("CK_PaymentReceipt_Amount", "\"Amount\" >= 0 AND \"Amount\" = FLOOR(\"Amount\") AND \"Currency\" = 'VND'");
            t.HasCheckConstraint("CK_PaymentReceipt_Source", "\"Source\" IN ('ManualBankTransfer','ManualCOD')");
        });
        receipt.Property(x => x.Source).HasMaxLength(32).IsUnicode(false).IsRequired();
        receipt.Property(x => x.Reference).HasMaxLength(100).IsUnicode(false).IsRequired();
        receipt.Property(x => x.NormalizedReference).HasMaxLength(100).IsUnicode(false).IsRequired();
        receipt.Property(x => x.RequestHash).HasMaxLength(64).IsUnicode(false).IsRequired();
        receipt.Property(x => x.Currency).HasMaxLength(3).IsUnicode(false).IsRequired();
        receipt.Property(x => x.Amount).HasPrecision(18, 2);
        receipt.HasIndex(x => x.OperationId).IsUnique();
        receipt.HasIndex(x => new { x.Source, x.NormalizedReference }).IsUnique();
        receipt.HasIndex(x => x.OrderId).IsUnique();
        receipt.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);

        // Fulfillment kind is part of these keys so a Unique item cannot bypass its
        // filtered single-delivery constraint by claiming to be Shared content.
        m.Entity<OrderItem>().HasAlternateKey(x => new { x.Id, x.OrderId, x.VariantId, x.FulfillmentKind });
        m.Entity<DigitalStockItem>().HasAlternateKey(x => new { x.Id, x.VariantId, x.FulfillmentKind });
        var delivery = m.Entity<DigitalDelivery>();
        delivery.ToTable("DigitalDeliveries", "Ordering", t =>
        {
            t.HasCheckConstraint("CK_DigitalDelivery_FulfillmentKind", "\"FulfillmentKind\" IN (2,3)");
            t.HasCheckConstraint("CK_DigitalDelivery_Sequence", "\"Sequence\" BETWEEN 1 AND 999");
        });
        delivery.Property(x => x.ProtectedContent).HasMaxLength(16000).IsRequired();
        delivery.HasIndex(x => new { x.OrderItemId, x.Sequence }).IsUnique();
        delivery.HasIndex(x => x.StockItemId).IsUnique()
            .HasFilter(postgres ? "\"FulfillmentKind\" = 2" : "[FulfillmentKind] = 2");
        delivery.HasOne<OrderItem>().WithMany()
            .HasForeignKey(x => new { x.OrderItemId, x.OrderId, x.VariantId, x.FulfillmentKind })
            .HasPrincipalKey(x => new { x.Id, x.OrderId, x.VariantId, x.FulfillmentKind })
            .OnDelete(DeleteBehavior.Restrict);
        delivery.HasOne<DigitalStockItem>().WithMany()
            .HasForeignKey(x => new { x.StockItemId, x.VariantId, x.FulfillmentKind })
            .HasPrincipalKey(x => new { x.Id, x.VariantId, x.FulfillmentKind })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
