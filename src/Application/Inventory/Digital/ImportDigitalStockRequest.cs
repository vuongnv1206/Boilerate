using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Identity.Users;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using Boilerate.Domain.DigitalInventory;
using MediatR;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Inventory.Digital;

public sealed record ImportDigitalStockRequest(
    Guid ProductId,
    Guid VariantId,
    string Version,
    List<DigitalStockInput> Items) : IRequest<List<DigitalStockDto>>;

public sealed class ImportDigitalStockRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work,
    ICurrentUser actor,
    IUserService users,
    IDataProtectionProvider protection) : IRequestHandler<ImportDigitalStockRequest, List<DigitalStockDto>>
{
    public async Task<List<DigitalStockDto>> Handle(ImportDigitalStockRequest request, CancellationToken ct)
    {
        await DigitalInventoryHelper.RequirePermissionAsync(users, actor, "Import", ct);
        if (request.Items is null || request.Items.Count is < 1 or > 100 || request.Items.Any(i => i is null))
        {
            throw new DomainException("Import requires between 1 and 100 digital stock items.");
        }

        var references = request.Items.Select(i => CatalogRules.Code(i.Reference).ToUpperInvariant()).ToArray();
        if (references.Distinct(StringComparer.Ordinal).Count() != references.Length)
        {
            throw new ConflictException("Duplicate digital stock references in import.");
        }

        foreach (var item in request.Items)
        {
            DigitalStockItem.ValidateContent(item.Content);
        }

        return await work.ExecuteAsync(async () =>
        {
            var product = await DigitalInventoryHelper.LockDigitalProductAsync(db, request.ProductId, request.Version, ct);
            DigitalInventoryHelper.RequireDigitalVariant(product, request.VariantId, ProductFulfillmentKind.DigitalUnique);
            if (await db.DigitalStockItems.AnyAsync(i => i.VariantId == request.VariantId && references.Contains(i.Reference), ct))
            {
                throw new ConflictException("A digital stock reference already exists for this SKU.");
            }

            var imported = request.Items.Select((input, index) =>
            {
                var id = Guid.NewGuid();
                return new DigitalStockItem(
                    id,
                    product.Id,
                    request.VariantId,
                    product.FulfillmentKind,
                    references[index],
                    DigitalInventoryHelper.CreateProtector(protection, id).Protect(input.Content));
            }).ToList();
            db.DigitalStockItems.AddRange(imported);
            product.Touch();
            return imported.Select(item => DigitalStockDto.FromEntity(item, product.Version)).ToList();
        }, ct);
    }
}
