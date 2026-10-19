using Boilerate.Domain.Inventory;
using Boilerate.Domain.Ordering;
using Boilerate.Domain.Payments;
using Boilerate.Domain.Shipping;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Infrastructure.Persistence.Configuration;

internal static class AfterSalesModel
{
    internal static void Configure(ModelBuilder m, bool postgres)
    {
        foreach (var type in new[] { typeof(Refund), typeof(PhysicalReturn) })
        {
            var entity = m.Entity(type);
            entity.HasBaseType((Type?)null);
            entity.HasKey("Id");
            entity.Property<Guid>("Id").ValueGeneratedNever();
            entity.Property("Version").HasMaxLength(32).IsConcurrencyToken();
            entity.Property("RequestHash").HasMaxLength(64).IsUnicode(false).IsRequired();
            entity.Property("Reference").HasMaxLength(100).IsUnicode(false).IsRequired();
            entity.Property("NormalizedReference").HasMaxLength(100).IsUnicode(false).IsRequired();
            entity.Property("Reason").HasMaxLength(1000).IsRequired();
        }

        var refund = m.Entity<Refund>();
        refund.ToTable("Refunds", "Payments", t =>
        {
            t.HasCheckConstraint("CK_Refund_Amount", "\"Amount\" > 0 AND \"Amount\" = FLOOR(\"Amount\") AND \"Currency\" = 'VND'");
            t.HasCheckConstraint("CK_Refund_Source", "\"Source\" IN ('ManualBankTransfer','ManualCash')");
        });
        refund.Property(x => x.Amount).HasPrecision(18, 2);
        refund.Property(x => x.Currency).HasMaxLength(3).IsUnicode(false).IsRequired();
        refund.Property(x => x.Source).HasMaxLength(32).IsUnicode(false).IsRequired();
        refund.HasIndex(x => x.OperationId).IsUnique();
        refund.HasIndex(x => new { x.Source, x.NormalizedReference }).IsUnique();
        refund.HasIndex(x => new { x.OrderId, x.RefundedAtUtc, x.Id });
        refund.HasOne<PaymentReceipt>().WithMany()
            .HasForeignKey(x => new { x.ReceiptId, x.OrderId, x.Currency })
            .HasPrincipalKey(x => new { x.Id, x.OrderId, x.Currency })
            .OnDelete(DeleteBehavior.Restrict);

        var physicalReturn = m.Entity<PhysicalReturn>();
        physicalReturn.ToTable("PhysicalReturns", "Shipping");
        physicalReturn.HasAlternateKey(x => new { x.Id, x.OrderId });
        physicalReturn.HasIndex(x => x.OperationId).IsUnique();
        physicalReturn.HasIndex(x => x.NormalizedReference).IsUnique();
        physicalReturn.HasIndex(x => new { x.OrderId, x.ReceivedAtUtc, x.Id });
        physicalReturn.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);

        var item = m.Entity<PhysicalReturnItem>();
        item.ToTable("PhysicalReturnItems", "Shipping", t =>
        {
            t.HasCheckConstraint("CK_PhysicalReturnItem_Quantity", "\"Quantity\" BETWEEN 1 AND 999 AND \"RestockQuantity\" >= 0 AND \"RestockQuantity\" <= \"Quantity\"");
            t.HasCheckConstraint("CK_PhysicalReturnItem_Movement", "(\"RestockQuantity\" = 0 AND \"StockMovementId\" IS NULL) OR (\"RestockQuantity\" > 0 AND \"StockMovementId\" IS NOT NULL)");
        });
        item.HasKey(x => new { x.ReturnId, x.ShipmentId, x.OrderItemId });
        item.HasOne<PhysicalReturn>().WithMany(x => x.Items)
            .HasForeignKey(x => new { x.ReturnId, x.OrderId })
            .HasPrincipalKey(x => new { x.Id, x.OrderId }).OnDelete(DeleteBehavior.Restrict);
        item.HasOne<ShipmentItem>().WithMany()
            .HasForeignKey(x => new { x.ShipmentId, x.OrderItemId, x.OrderId, x.VariantId })
            .HasPrincipalKey(x => new { x.ShipmentId, x.OrderItemId, x.OrderId, x.VariantId })
            .OnDelete(DeleteBehavior.Restrict);
        item.HasOne<StockMovement>().WithMany().HasForeignKey(x => x.StockMovementId).OnDelete(DeleteBehavior.Restrict);
        item.HasIndex(x => x.StockMovementId).IsUnique()
            .HasFilter(postgres ? "\"StockMovementId\" IS NOT NULL" : "[StockMovementId] IS NOT NULL");
    }
}
