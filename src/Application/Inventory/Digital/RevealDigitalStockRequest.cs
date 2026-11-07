using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Identity.Users;
using Boilerate.Domain.Auditing;
using MediatR;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Inventory.Digital;

public sealed record RevealDigitalStockRequest(Guid StockItemId) : IRequest<DigitalSecretDto>;

public sealed class RevealDigitalStockRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work,
    ICurrentUser actor,
    IUserService users,
    IDataProtectionProvider protection) : IRequestHandler<RevealDigitalStockRequest, DigitalSecretDto>
{
    public async Task<DigitalSecretDto> Handle(RevealDigitalStockRequest request, CancellationToken ct)
    {
        await DigitalInventoryHelper.RequirePermissionAsync(users, actor, "Reveal", ct);
        return await work.ExecuteAsync(async () =>
        {
            var item = await db.DigitalStockItems.AsNoTracking().SingleOrDefaultAsync(i => i.Id == request.StockItemId, ct)
                ?? throw new NotFoundException("Digital stock item not found.");
            string content = DigitalInventoryHelper.CreateProtector(protection, item.Id).Unprotect(item.ProtectedContent);
            db.Trails.Add(new Trail
            {
                UserId = actor.GetUserId(),
                TableName = "DigitalStockItems",
                PrimaryKey = item.Id.ToString(),
                Type = TrailType.Read,
                DateTime = DateTime.UtcNow,
                NewValues = "{\"Action\":\"Reveal\"}",
            });
            return new DigitalSecretDto(item.Id, content);
        }, ct);
    }
}
