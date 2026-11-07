using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Ordering.Shared;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Shipping;

public sealed record GetMyOrderShipmentsRequest(Guid OrderId) : IRequest<List<ShipmentDto>>;

public sealed class GetMyOrderShipmentsRequestValidator : CustomValidator<GetMyOrderShipmentsRequest>
{
    public GetMyOrderShipmentsRequestValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
    }
}

public sealed class GetMyOrderShipmentsRequestHandler(
    IApplicationDbContext db,
    ICurrentUser actor) : IRequestHandler<GetMyOrderShipmentsRequest, List<ShipmentDto>>
{
    public async Task<List<ShipmentDto>> Handle(GetMyOrderShipmentsRequest request, CancellationToken ct)
    {
        Guid customer = OrderingAuthHelper.RequireCustomer(actor);
        var order = await db.Orders.AsNoTracking()
            .SingleOrDefaultAsync(o => o.Id == request.OrderId && o.CustomerId == customer, ct)
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

