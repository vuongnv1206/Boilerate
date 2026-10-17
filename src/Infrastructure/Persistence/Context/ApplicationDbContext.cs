using Boilerate.Application.Common.Events;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Domain.Auditing;
using Boilerate.Domain.Identity;
using Boilerate.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

using Boilerate.Domain.Catalog;

namespace Boilerate.Infrastructure.Persistence.Context;

public class ApplicationDbContext : BaseDbContext
{
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentUser currentUser,
        ISerializerService serializer,
        IEventPublisher events)
        : base(options, currentUser, serializer, events)
    {
    }

    // Catalog
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();

    public DbSet<ProductType> ProductTypes => Set<ProductType>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<Boilerate.Domain.DigitalInventory.DigitalStockItem> DigitalStockItems => Set<Boilerate.Domain.DigitalInventory.DigitalStockItem>();
    public DbSet<Boilerate.Domain.Inventory.InventoryItem> InventoryItems => Set<Boilerate.Domain.Inventory.InventoryItem>();
    public DbSet<Boilerate.Domain.Inventory.StockMovement> StockMovements => Set<Boilerate.Domain.Inventory.StockMovement>();
    public DbSet<Boilerate.Domain.Media.MediaAsset> MediaAssets => Set<Boilerate.Domain.Media.MediaAsset>();
    public DbSet<Boilerate.Domain.Messaging.OutboxMessage> OutboxMessages => Set<Boilerate.Domain.Messaging.OutboxMessage>();

    // Auditing
    public DbSet<Trail> Trails => Set<Trail>();

    // Permission system DbSets
    public DbSet<Domain.Identity.Action> Actions => Set<Domain.Identity.Action>();
    public DbSet<Function> Functions => Set<Function>();
    public DbSet<ActionInFunction> ActionInFunctions => Set<ActionInFunction>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationReceipt> NotificationReceipts => Set<NotificationReceipt>();
    public DbSet<AuthorizationLock> AuthorizationLocks => Set<AuthorizationLock>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
}
