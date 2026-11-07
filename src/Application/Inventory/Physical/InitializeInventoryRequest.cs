using Boilerate.Application.Common.Persistence;
using Boilerate.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Inventory.Physical;

public sealed record InitializeInventoryRequest : IRequest<Guid>;

public sealed class InitializeInventoryRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work) : IRequestHandler<InitializeInventoryRequest, Guid>
{
    public Task<Guid> Handle(InitializeInventoryRequest request, CancellationToken ct) =>
        work.ExecuteAsync(async () =>
        {
            await CommerceConcurrency.AcquireGateAsync(db, "initialization", ct);
            var existing = await db.Set<InventoryLocation>().SingleOrDefaultAsync(l => l.Code == "MAIN", ct);
            if (existing is not null)
            {
                return existing.Id;
            }

            var main = new InventoryLocation("MAIN", "Main warehouse");
            db.Add(main);
            return main.Id;
        }, ct);
}
