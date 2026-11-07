using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Identity.Users;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.DigitalInventory;
using Boilerate.Domain.Inventory;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Overview;

public sealed record GetOverviewStockRequest(int Threshold = 5, int Page = 1, int Size = 10) : IRequest<OverviewStockDto>;

public sealed class GetOverviewStockRequestValidator : CustomValidator<GetOverviewStockRequest>
{
    public GetOverviewStockRequestValidator()
    {
        RuleFor(x => x.Threshold).InclusiveBetween(0, 999);
        RuleFor(x => x.Size).Must(size => size is 10 or 20 or 50);
        RuleFor(x => x)
            .Must(x => x.Page >= 1 && x.Size > 0 && x.Page <= int.MaxValue / x.Size)
            .WithMessage("Invalid stock threshold or page.");
    }
}

public sealed class GetOverviewStockRequestHandler(
    IApplicationDbContext db,
    ICurrentUser actor,
    IUserService users,
    TimeProvider clock) : IRequestHandler<GetOverviewStockRequest, OverviewStockDto>
{
    public async Task<OverviewStockDto> Handle(GetOverviewStockRequest request, CancellationToken ct)
    {
        if (!actor.IsAuthenticated() || actor.GetUserId() == Guid.Empty)
        {
            throw new UnauthorizedException("Sign in to view the overview.");
        }

        string userId = actor.GetUserId().ToString();
        bool physical = await users.HasPermissionAsync(userId, "Permissions.Inventory.View", ct);
        bool digital = await users.HasPermissionAsync(userId, "Permissions.DigitalInventory.View", ct);
        if (!physical && !digital)
        {
            throw new ForbiddenException("Inventory.View or DigitalInventory.View is required.");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var query =
            from variant in db.ProductVariants.AsNoTracking()
            join product in db.Products.AsNoTracking() on variant.ProductId equals product.Id
            where variant.IsActive && !variant.IsRetired && product.Status == ProductStatus.Published &&
                ((physical && product.FulfillmentKind == ProductFulfillmentKind.Physical && variant.TrackInventory) ||
                 (digital && (product.FulfillmentKind == ProductFulfillmentKind.DigitalUnique ||
                              product.FulfillmentKind == ProductFulfillmentKind.DigitalShared)))
            select new StockProjection
            {
                ProductId = product.Id,
                VariantId = variant.Id,
                ProductName = product.Name,
                Sku = variant.Sku,
                FulfillmentKind = product.FulfillmentKind,
                Available = product.FulfillmentKind == ProductFulfillmentKind.Physical
                    ? db.InventoryItems.Where(i => i.VariantId == variant.Id &&
                        db.Set<InventoryLocation>().Any(l => l.Id == i.LocationId && l.Code == "MAIN"))
                        .Select(i => (int?)(i.OnHand - i.Reserved)).FirstOrDefault() ?? 0
                    : db.DigitalStockItems.Count(i => i.VariantId == variant.Id && i.State == DigitalStockState.Available),
            };
        query = query.Where(i => i.FulfillmentKind == ProductFulfillmentKind.DigitalShared
            ? i.Available == 0
            : i.Available <= request.Threshold);
        int total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(i => i.Available)
            .ThenBy(i => i.ProductName)
            .ThenBy(i => i.VariantId)
            .Skip((request.Page - 1) * request.Size)
            .Take(request.Size)
            .Select(i => new OverviewStockItem(i.ProductId, i.VariantId, i.ProductName, i.Sku, i.FulfillmentKind, i.Available))
            .ToListAsync(ct);
        return new(now, request.Threshold, request.Page, request.Size, total, items);
    }

    private sealed class StockProjection
    {
        public Guid ProductId { get; init; }
        public Guid VariantId { get; init; }
        public string ProductName { get; init; } = string.Empty;
        public string Sku { get; init; } = string.Empty;
        public ProductFulfillmentKind FulfillmentKind { get; init; }
        public int Available { get; init; }
    }
}
