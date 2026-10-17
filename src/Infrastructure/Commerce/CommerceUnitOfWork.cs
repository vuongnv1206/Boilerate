using Boilerate.Infrastructure.Persistence.Context;
using Boilerate.Domain.Common.Contracts;
namespace Boilerate.Infrastructure.Commerce;

public sealed class CommerceUnitOfWork(ApplicationDbContext db)
{
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("Commerce use cases own their transaction.");
        if (db.ChangeTracker.HasChanges() || db.ChangeTracker.Entries<IEntity>().Any(e => e.Entity.DomainEvents.Count != 0))
            throw new InvalidOperationException("Save or discard pending changes before starting a commerce command.");
        db.ChangeTracker.Clear(); // Every command loads a fresh aggregate after acquiring its locks.
        T result;
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            try
            {
                result = await operation();
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(CancellationToken.None);
                throw;
            }
        }
        catch
        {
            // The external transaction must be disposed before completing its event batch.
            db.DiscardPendingEvents();
            db.ChangeTracker.Clear();
            throw;
        }
        // Handler failures happen after commit and must never enter the rollback path.
        await db.DispatchCommittedEventsAsync(ct);
        return result;
    }
}
