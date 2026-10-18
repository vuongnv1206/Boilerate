using Boilerate.Application.Common.Events;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Domain.Auditing;
using Boilerate.Domain.Common.Contracts;
using Boilerate.Domain.Identity;
using Boilerate.Infrastructure.Auditing;
using Boilerate.Infrastructure.Persistence.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Infrastructure.Persistence.Context;

/// <summary>
/// Base DbContext với domain events và soft delete support.
/// </summary>
public abstract class BaseDbContext : IdentityDbContext<
    ApplicationUser,
    ApplicationRole,
    string,
    IdentityUserClaim<string>,
    IdentityUserRole<string>,
    IdentityUserLogin<string>,
    ApplicationRoleClaim,
    IdentityUserToken<string>>
{
    private readonly ICurrentUser _currentUser;
    private readonly ISerializerService _serializer;
    private readonly IEventPublisher _events;
    private readonly Queue<PendingEvent> _committedEvents = new();
    private readonly List<PendingEvent> _externalTransactionEvents = new();
    private Guid? _externalTransactionId;
    private bool _dispatchingEvents;

    protected BaseDbContext(
        DbContextOptions options,
        ICurrentUser currentUser,
        ISerializerService serializer,
        IEventPublisher events)
        : base(options)
    {
        _currentUser = currentUser;
        _serializer = serializer;
        _events = events;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // QueryFilters need to be applied before base.OnModelCreating
        modelBuilder.AppendGlobalQueryFilter<ISoftDelete>(e => e.DeletedOn == null);

        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
        Boilerate.Infrastructure.Persistence.Configuration.CommerceModel.Configure(modelBuilder, Database.IsNpgsql());
        Boilerate.Infrastructure.Persistence.Configuration.OrderingModel.Configure(modelBuilder, Database.IsNpgsql());
        Boilerate.Infrastructure.Persistence.Configuration.PaymentModel.Configure(modelBuilder, Database.IsNpgsql());
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Sensitive values must never be enabled implicitly for every environment.
    }

    // Event handlers are asynchronous. Reject sync entry points instead of silently bypassing
    // auditing, soft delete and event dispatch through DbContext's other virtual overloads.
    public override int SaveChanges() => throw SynchronousSaveNotSupported();

    public override int SaveChanges(bool acceptAllChangesOnSuccess) => throw SynchronousSaveNotSupported();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        if (!acceptAllChangesOnSuccess)
        {
            // Audit rows may require a second save for store-generated keys. Leaving the first
            // batch unaccepted would submit those same changes again in that second save.
            throw new NotSupportedException("Deferred change acceptance is not supported by the audited context. Use SaveChangesAsync(cancellationToken).");
        }

        ChangeTracker.DetectChanges();
        if (ChangeTracker.Entries<Boilerate.Domain.Inventory.StockMovement>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Stock movements are append-only; use a compensating movement.");
        if (ChangeTracker.Entries<Boilerate.Domain.Catalog.Product>().Any(e => e.State == EntityState.Deleted) ||
            ChangeTracker.Entries<Boilerate.Domain.Catalog.ProductVariant>().Any(e => e.State == EntityState.Deleted))
            throw new InvalidOperationException("Archive products/SKUs instead of deleting commerce history.");
        if (ChangeTracker.Entries<Boilerate.Domain.DigitalInventory.DigitalStockItem>().Any(e => e.State == EntityState.Deleted))
            throw new InvalidOperationException("Disable digital stock items instead of deleting commerce history.");
        if (ChangeTracker.Entries<Boilerate.Domain.Ordering.OrderItem>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Order item snapshots are immutable.");
        if (ChangeTracker.Entries<Boilerate.Domain.Payments.PaymentReceipt>().Any(e => e.State is EntityState.Modified or EntityState.Deleted) ||
            ChangeTracker.Entries<Boilerate.Domain.Ordering.DigitalDelivery>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Payment receipts and digital deliveries are append-only history.");
        if (ChangeTracker.Entries<Boilerate.Domain.Ordering.Cart>().Any(e => e.State == EntityState.Deleted) ||
            ChangeTracker.Entries<Boilerate.Domain.Ordering.Order>().Any(e => e.State == EntityState.Deleted) ||
            ChangeTracker.Entries<Boilerate.Domain.Ordering.PhysicalStockReservation>().Any(e => e.State == EntityState.Deleted))
            throw new InvalidOperationException("Retain ordering history instead of deleting carts, orders or reservations.");
        var ownsTransaction = Database.CurrentTransaction is null;
        var externalTransaction = Database.CurrentTransaction;
        if (_externalTransactionId.HasValue && _externalTransactionId != externalTransaction?.TransactionId)
        {
            throw new InvalidOperationException("Complete the previous external event batch before saving again: dispatch after commit/dispose, or discard after rollback/dispose.");
        }

        // Capture exact events before SaveChanges detaches physically deleted entities. Events
        // raised later must remain queued on their source until a later successful save.
        ChangeTracker.DetectChanges();
        var pendingEvents = ChangeTracker.Entries<IEntity>()
            .SelectMany(entry => entry.Entity.DomainEvents.Select(domainEvent => new PendingEvent(entry.Entity, domainEvent)))
            .ToArray();
        int result;
        await using (var transaction = ownsTransaction ? await Database.BeginTransactionAsync(cancellationToken) : null)
        {
            result = await SaveAuditedChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }

        foreach (var pendingEvent in pendingEvents)
        {
            pendingEvent.Source.DomainEvents.Remove(pendingEvent.Event);
            if (ownsTransaction)
            {
                _committedEvents.Enqueue(pendingEvent);
            }
            else
            {
                _externalTransactionEvents.Add(pendingEvent);
            }
        }

        if (!ownsTransaction && pendingEvents.Length != 0)
        {
            _externalTransactionId = externalTransaction!.TransactionId;
        }

        if (ownsTransaction)
        {
            // The owned transaction is disposed before handlers may perform another write.
            await DispatchReadyEventsAsync(cancellationToken);
        }

        return result;
    }

    private static NotSupportedException SynchronousSaveNotSupported() =>
        new("Use SaveChangesAsync(cancellationToken) so auditing, soft delete and asynchronous domain events run together.");

    private async Task<int> SaveAuditedChangesAsync(CancellationToken cancellationToken)
    {
        // 1. Handle soft delete and auditing metadata (CreatedOn, DeletedOn, etc.)
        HandleAuditingMetadataBeforeSaveChanges();

        // Ensure EF Core captures all changes before we analyze them for Audit Trail
        ChangeTracker.DetectChanges();

        // 2. Gather audit entries (Giai đoạn chuẩn bị)
        var auditEntries = AuditTrailHelper.GatherAuditEntries(ChangeTracker, _currentUser.GetUserId(), _serializer);

        // 3. Publish domain events
        // Events are dispatched after commit below; explicit transaction owners can call DispatchCommittedEventsAsync.

        // 4. Save audit entries that don't depend on DB-generated values
        foreach (var entry in auditEntries.Where(e => !e.HasTemporaryProperties))
        {
            Set<Trail>().Add(entry.ToAuditTrail());
        }

        // 5. First Save: Lưu dữ liệu chính và các Audit Logs không phụ thuộc ID DB
        int result = await base.SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);

        // 6. Handle Audit entries with temporary properties (Giai đoạn hoàn tất)
        // Những record mới thêm có ID do DB sinh ra sẽ được xử lý ở đây
        if (auditEntries.Any(e => e.HasTemporaryProperties))
        {
            foreach (var entry in auditEntries.Where(e => e.HasTemporaryProperties))
            {
                foreach (var prop in entry.TemporaryProperties)
                {
                    if (prop.Metadata.IsPrimaryKey())
                    {
                        entry.KeyValues[prop.Metadata.Name] = prop.CurrentValue;
                    }
                    else
                    {
                        entry.NewValues[prop.Metadata.Name] = prop.CurrentValue;
                    }
                }

                Set<Trail>().Add(entry.ToAuditTrail());
            }

            // Lưu nốt phần Audit Logs còn lại
            await base.SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);
        }

        return result;
    }

    /// <summary>
    /// Handle soft delete logic và cập nhật audit metadata (CreatedOn, LastModifiedOn, etc.).
    /// </summary>
    private void HandleAuditingMetadataBeforeSaveChanges()
    {
        var userId = _currentUser.GetUserId();

        foreach (var entry in ChangeTracker.Entries<IAuditableEntity>().ToList())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedBy = userId;
                    entry.Entity.CreatedOn = DateTime.UtcNow;
                    break;

                case EntityState.Modified:
                    entry.Entity.LastModifiedBy = userId;
                    entry.Entity.LastModifiedOn = DateTime.UtcNow;
                    break;

                case EntityState.Deleted:
                    // ⭐ SOFT DELETE LOGIC ⭐
                    if (entry.Entity is ISoftDelete softDelete)
                    {
                        softDelete.DeletedOn = DateTime.UtcNow;
                        softDelete.DeletedBy = userId;
                        entry.State = EntityState.Modified;
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// Confirms that the caller committed and disposed its external transaction, then dispatches
    /// that saved event batch. Call DiscardPendingEvents instead after rollback.
    /// </summary>
    public async Task DispatchCommittedEventsAsync(CancellationToken cancellationToken = default)
    {
        RequireCompletedTransaction();
        foreach (var pendingEvent in _externalTransactionEvents)
        {
            _committedEvents.Enqueue(pendingEvent);
        }

        _externalTransactionEvents.Clear();
        _externalTransactionId = null;
        await DispatchReadyEventsAsync(cancellationToken);
    }

    /// <summary>Discards events from an external transaction the caller rolled back and disposed.</summary>
    public void DiscardPendingEvents()
    {
        RequireCompletedTransaction();
        _externalTransactionEvents.Clear();
        _externalTransactionId = null;
    }

    private void RequireCompletedTransaction()
    {
        if (Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException("Complete and dispose the external transaction before completing its event batch.");
        }
    }

    private async Task DispatchReadyEventsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_dispatchingEvents)
        {
            return;
        }

        _dispatchingEvents = true;
        try
        {
            while (_committedEvents.TryPeek(out var pendingEvent))
            {
                cancellationToken.ThrowIfCancellationRequested();
                // A handler may partially succeed before throwing. Do not implicitly retry that
                // attempted event; keep unattempted events for a later drain (explicit or after save).
                _committedEvents.Dequeue();
                await _events.PublishAsync(pendingEvent.Event);
            }
        }
        finally
        {
            _dispatchingEvents = false;
        }
    }

    private sealed record PendingEvent(IEntity Source, DomainEvent Event);
}
