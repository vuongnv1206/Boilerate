using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Identity.Users;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.DigitalInventory;
using MediatR;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Inventory.Digital;

public sealed record SaveSharedDigitalContentRequest(
    Guid ProductId,
    Guid VariantId,
    string Version,
    string Content,
    bool IsActive = true) : IRequest<DigitalStockDto>;

public sealed class SaveSharedDigitalContentRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work,
    ICurrentUser actor,
    IUserService users,
    IDataProtectionProvider protection) : IRequestHandler<SaveSharedDigitalContentRequest, DigitalStockDto>
{
    public async Task<DigitalStockDto> Handle(SaveSharedDigitalContentRequest request, CancellationToken ct)
    {
        await DigitalInventoryHelper.RequirePermissionAsync(users, actor, "Update", ct);
        DigitalStockItem.ValidateContent(request.Content);
        return await work.ExecuteAsync(async () =>
        {
            var product = await DigitalInventoryHelper.LockDigitalProductAsync(db, request.ProductId, request.Version, ct);
            DigitalInventoryHelper.RequireDigitalVariant(product, request.VariantId, ProductFulfillmentKind.DigitalShared);
            var item = await db.DigitalStockItems.SingleOrDefaultAsync(i => i.VariantId == request.VariantId, ct);
            if (item is null)
            {
                var id = Guid.NewGuid();
                item = new(
                    id,
                    product.Id,
                    request.VariantId,
                    product.FulfillmentKind,
                    "SHARED",
                    DigitalInventoryHelper.CreateProtector(protection, id).Protect(request.Content));
                db.DigitalStockItems.Add(item);
            }
            else
            {
                item.ReplaceSharedContent(DigitalInventoryHelper.CreateProtector(protection, item.Id).Protect(request.Content));
            }

            item.SetAvailability(request.IsActive);
            product.Touch();
            return DigitalStockDto.FromEntity(item, product.Version);
        }, ct);
    }
}
