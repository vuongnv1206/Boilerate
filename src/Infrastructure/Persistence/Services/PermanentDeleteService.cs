using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Domain.Auditing;
using Boilerate.Domain.Common.Contracts;
using Boilerate.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
namespace Boilerate.Infrastructure.Persistence.Services;
public sealed class PermanentDeleteService<T>(ApplicationDbContext db, ICurrentUser actor, ISerializerService serializer) : IPermanentDeleteService<T>
    where T : class, IAggregateRoot
{
    public async Task PermanentDeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        if (!actor.IsAuthenticated()) throw new UnauthorizedException("Authentication required.");
        if (entity is not IEntity<Guid> keyed || entity is not ISoftDelete { DeletedOn: not null })
            throw new ConflictException("Only an already soft-deleted Guid entity can be permanently deleted.");
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var deleted = await db.Set<T>().IgnoreQueryFilters().Where(x => EF.Property<Guid>(x, "Id") == keyed.Id && EF.Property<DateTime?>(x, "DeletedOn") != null)
            .ExecuteDeleteAsync(cancellationToken);
        if (deleted != 1) throw new ConflictException("Entity changed before permanent deletion.");
        db.Trails.Add(new Trail { UserId = actor.GetUserId(), TableName = typeof(T).Name,
            PrimaryKey = keyed.Id.ToString(), Type = TrailType.Delete,
            OldValues = serializer.Serialize(new { keyed.Id, Permanent = true }) });
        db.Entry(entity).State = EntityState.Detached;
        await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken);
    }
}
