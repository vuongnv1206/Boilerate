using Boilerate.Application.Common.Interfaces;
using Boilerate.Domain.Auditing;
using Boilerate.Domain.Common.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Boilerate.Infrastructure.Auditing;

/// <summary>
/// Helper để xử lý logic Audit Trail.
/// </summary>
public static class AuditTrailHelper
{
    /// <summary>
    /// Thu thập và phân tích các entries đang thay đổi để chuẩn bị tạo Audit Log.
    /// </summary>
    public static List<AuditTrail> GatherAuditEntries(
        ChangeTracker changeTracker, 
        Guid userId, 
        ISerializerService serializer)
    {
        var trailEntries = new List<AuditTrail>();

        foreach (var entry in changeTracker.Entries().Where(e => e.Entity is IAuditableEntity ||
            e.Entity is Boilerate.Domain.Ordering.CartItem or Boilerate.Domain.Ordering.OrderItem ||
            (e.Entity.GetType().Namespace == "Boilerate.Domain.Catalog" && e.Entity is not Boilerate.Domain.Catalog.CommerceEntity)))
        {
            if (entry.State is EntityState.Detached or EntityState.Unchanged)
                continue;

            var trailEntry = new AuditTrail(entry, serializer)
            {
                TableName = entry.Metadata.GetTableName() ?? entry.Entity.GetType().Name,
                UserId = userId
            };
            trailEntries.Add(trailEntry);

            foreach (var property in entry.Properties)
            {
                // Digital stock contents remain secret even in their encrypted representation.
                // The entity version and metadata retain the audit history for content changes.
                if ((entry.Entity is Boilerate.Domain.DigitalInventory.DigitalStockItem or Boilerate.Domain.Ordering.DigitalDelivery) &&
                    property.Metadata.Name == nameof(Boilerate.Domain.DigitalInventory.DigitalStockItem.ProtectedContent))
                    continue;

                // Contact information belongs to the order, not to a second copy in audit JSON.
                if (entry.Entity is Boilerate.Domain.Ordering.Order && property.Metadata.Name is
                    nameof(Boilerate.Domain.Ordering.Order.RecipientName) or
                    nameof(Boilerate.Domain.Ordering.Order.Email) or
                    nameof(Boilerate.Domain.Ordering.Order.Phone) or
                    nameof(Boilerate.Domain.Ordering.Order.ShippingAddress))
                    continue;

                // Nếu property có giá trị tạm thời (sẽ được DB sinh ra sau khi Save)
                if (property.IsTemporary)
                {
                    trailEntry.TemporaryProperties.Add(property);
                    continue;
                }

                string propertyName = property.Metadata.Name;

                // Lưu Primary Key
                if (property.Metadata.IsPrimaryKey())
                {
                    trailEntry.KeyValues[propertyName] = property.CurrentValue;
                    continue;
                }

                // Skip navigation/foreign keys nếu muốn gọn log
                if (property.Metadata.IsForeignKey() && entry.Entity.GetType().Namespace is not ("Boilerate.Domain.Catalog" or "Boilerate.Domain.Inventory" or "Boilerate.Domain.Media" or "Boilerate.Domain.DigitalInventory" or "Boilerate.Domain.Ordering" or "Boilerate.Domain.Payments")) continue;

                switch (entry.State)
                {
                    case EntityState.Added:
                        trailEntry.TrailType = TrailType.Create;
                        trailEntry.NewValues[propertyName] = property.CurrentValue;
                        break;

                    case EntityState.Deleted:
                        trailEntry.TrailType = TrailType.Delete;
                        trailEntry.OldValues[propertyName] = property.OriginalValue;
                        break;

                    case EntityState.Modified:
                        // ⭐ SOFT DELETE DETECTION ⭐
                        if (property.IsModified && 
                            entry.Entity is ISoftDelete && 
                            propertyName == nameof(ISoftDelete.DeletedOn) &&
                            property.OriginalValue == null && 
                            property.CurrentValue != null)
                        {
                            trailEntry.ChangedColumns.Add(propertyName);
                            trailEntry.ChangedColumns.Add(nameof(ISoftDelete.DeletedBy));
                            trailEntry.TrailType = TrailType.Delete;
                            trailEntry.OldValues[propertyName] = property.OriginalValue;
                            trailEntry.NewValues[propertyName] = property.CurrentValue;
                        }
                        else if (property.IsModified && !Equals(property.OriginalValue, property.CurrentValue))
                        {
                            trailEntry.ChangedColumns.Add(propertyName);
                            trailEntry.TrailType = trailEntry.TrailType == TrailType.Delete ? TrailType.Delete : TrailType.Update;
                            trailEntry.OldValues[propertyName] = property.OriginalValue;
                            trailEntry.NewValues[propertyName] = property.CurrentValue;
                        }
                        break;
                }
            }
        }

        return trailEntries;
    }
}
