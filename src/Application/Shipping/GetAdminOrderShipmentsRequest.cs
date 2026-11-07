using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Identity.Users;
using Boilerate.Application.Ordering.Shared;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Shipping;

public sealed record GetAdminOrderShipmentsRequest(Guid OrderId) : IRequest<List<ShipmentDto>>;

public sealed class GetAdminOrderShipmentsRequestValidator : CustomValidator<GetAdminOrderShipmentsRequest>
{
    public GetAdminOrderShipmentsRequestValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
    }
}

public sealed class GetAdminOrderShipmentsRequestHandler(
    IApplicationDbContext db,
    ICurrentUser actor,
    IUserService users) : IRequestHandler<GetAdminOrderShipmentsRequest, List<ShipmentDto>>
{
    public async Task<List<ShipmentDto>> Handle(GetAdminOrderShipmentsRequest request, CancellationToken ct)
    {
        await OrderingAuthHelper.RequirePermissionAsync(actor, users, "Shipping", "View", ct);
        var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(o => o.Id == request.OrderId, ct)
            ?? throw new NotFoundException("Order not found.");
        var shipments = await ShipmentMapper.Shipments(db)
            .AsNoTracking()
            .Where(s => s.OrderId == request.OrderId)
            .OrderBy(s => s.DispatchedAtUtc)
            .ThenBy(s => s.Id)
            .ToListAsync(ct);
        return shipments.Select(s => ShipmentMapper.Map(s, order)).ToList();
    }
}

