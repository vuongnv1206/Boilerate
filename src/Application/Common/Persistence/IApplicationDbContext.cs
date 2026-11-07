using Boilerate.Domain.Auditing;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.DigitalInventory;
using Boilerate.Domain.Identity;
using Boilerate.Domain.Inventory;
using Boilerate.Domain.Media;
using Boilerate.Domain.Messaging;
using Boilerate.Domain.Ordering;
using Boilerate.Domain.Payments;
using Boilerate.Domain.Renewals;
using Boilerate.Domain.Shipping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Boilerate.Application.Common.Persistence;

public interface IApplicationDbContext
{
    DbSet<Product> Products { get; }
    DbSet<Category> Categories { get; }
    DbSet<ProductType> ProductTypes { get; }
    DbSet<ProductVariant> ProductVariants { get; }
    DbSet<DigitalStockItem> DigitalStockItems { get; }
    DbSet<InventoryItem> InventoryItems { get; }
    DbSet<StockMovement> StockMovements { get; }
    DbSet<MediaAsset> MediaAssets { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }
    DbSet<Cart> Carts { get; }
    DbSet<Order> Orders { get; }
    DbSet<OrderItem> OrderItems { get; }
    DbSet<PhysicalStockReservation> PhysicalStockReservations { get; }
    DbSet<PaymentReceipt> PaymentReceipts { get; }
    DbSet<DigitalDelivery> DigitalDeliveries { get; }
    DbSet<Shipment> Shipments { get; }
    DbSet<ShipmentItem> ShipmentItems { get; }
    DbSet<RenewalRequest> RenewalRequests { get; }
    DbSet<RenewalAttempt> RenewalAttempts { get; }
    DbSet<RenewalOperation> RenewalOperations { get; }
    DbSet<Refund> Refunds { get; }
    DbSet<PhysicalReturn> PhysicalReturns { get; }
    DbSet<PhysicalReturnItem> PhysicalReturnItems { get; }
    DbSet<Trail> Trails { get; }
    DbSet<ApplicationUser> Users { get; }

    DbSet<TEntity> Set<TEntity>()
        where TEntity : class;

    EntityEntry<TEntity> Entry<TEntity>(TEntity entity)
        where TEntity : class;

    EntityEntry<TEntity> Add<TEntity>(TEntity entity)
        where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    IQueryable<ProductFieldValue> FilterExactText(IQueryable<ProductFieldValue> query, string text);
}