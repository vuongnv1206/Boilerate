using Boilerate.Application.Common.Persistence;
using Boilerate.Domain.Ordering;
using Boilerate.Domain.Shipping;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Shipping;

public sealed record ShipmentLineInput(Guid OrderItemId, int Quantity);

public sealed record DispatchShipment(
    string OrderVersion,
    Guid OperationId,
    string Carrier,
    string TrackingNumber,
    List<ShipmentLineInput> Items);

public sealed record DeliverShipment(string OrderVersion, string Version, Guid OperationId);

public sealed record ShipmentLineDto(Guid OrderItemId, int Quantity);

public sealed record ShipmentDto(
    Guid Id,
    Guid OrderId,
    string OrderVersion,
    string Version,
    string Carrier,
    string TrackingNumber,
    ShipmentStatus Status,
    DateTime DispatchedAtUtc,
    DateTime? DeliveredAtUtc,
    List<ShipmentLineDto> Items);

internal static class ShipmentMapper
{
    internal static IQueryable<Shipment> Shipments(IApplicationDbContext db) =>
        db.Set<Shipment>().Include(s => s.Items);

    internal static ShipmentDto Map(Shipment shipment, Order order) =>
        new(
            shipment.Id,
            order.Id,
            order.Version,
            shipment.Version,
            shipment.Carrier,
            shipment.TrackingNumber,
            shipment.Status,
            CommerceConcurrency.UtcTimestamp(shipment.DispatchedAtUtc),
            CommerceConcurrency.UtcTimestamp(shipment.DeliveredAtUtc),
            shipment.Items
                .OrderBy(i => i.OrderItemId)
                .Select(i => new ShipmentLineDto(i.OrderItemId, i.Quantity))
                .ToList());
}

