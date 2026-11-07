using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Identity.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Inventory.Digital;

public sealed record SetDigitalStockAvailabilityRequest(
    Guid ProductId,
    Guid VariantId,
    Guid StockItemId,
    string Version,
    bool IsAvailable) : IRequest<DigitalStockDto>;

public sealed class SetDigitalStockAvailabilityRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work,
    ICurrentUser actor,
    IUserService users) : IRequestHandler<SetDigitalStockAvailabilityRequest, DigitalStockDto>
{
    public async Task<DigitalStockDto> Handle(SetDigitalStockAvailabilityRequest request, CancellationToken ct)
    {
        await DigitalInventoryHelper.RequirePermissionAsync(users, actor, "Update", ct);
        return await work.ExecuteAsync(async () =>
        {
            var product = await DigitalInventoryHelper.LockDigitalProductAsync(db, request.ProductId, request.Version, ct);
            _ = product.GetVariant(request.VariantId);
            var item = await db.DigitalStockItems
                .SingleOrDefaultAsync(i => i.Id == request.StockItemId && i.ProductId == product.Id && i.VariantId == request.VariantId, ct)
                ?? throw new NotFoundException("Digital stock item not found.");
            item.SetAvailability(request.IsAvailable);
            product.Touch();
            return DigitalStockDto.FromEntity(item, product.Version);
        }, ct);
    }
}
