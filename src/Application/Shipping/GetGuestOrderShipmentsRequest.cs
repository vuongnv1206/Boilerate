using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Ordering.Shared;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Shipping;

public sealed record GetGuestOrderShipmentsRequest(
    Guid OrderId,
    string VerifiedEmail) : IRequest<List<ShipmentDto>>;

public sealed class GetGuestOrderShipmentsRequestValidator : CustomValidator<GetGuestOrderShipmentsRequest>
{
    public GetGuestOrderShipmentsRequestValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
    }
}

public sealed class GetGuestOrderShipmentsRequestHandler(IApplicationDbContext db)
    : IRequestHandler<GetGuestOrderShipmentsRequest, List<ShipmentDto>>
{
    public async Task<List<ShipmentDto>> Handle(GetGuestOrderShipmentsRequest request, CancellationToken ct)
    {
        string email = OrderingAuthHelper.NormalizeEmail(request.VerifiedEmail);
        var order = await db.Orders.AsNoTracking()
            .SingleOrDefaultAsync(o => o.Id == request.OrderId && o.IsGuestOrder && o.NormalizedEmail == email, ct)
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

