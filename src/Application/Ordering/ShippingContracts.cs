using Boilerate.Domain.Shipping;

namespace Boilerate.Application.Ordering;

public sealed record ShipmentLineInput(Guid OrderItemId, int Quantity);
public sealed record DispatchShipment(string OrderVersion, Guid OperationId, string Carrier, string TrackingNumber,
    List<ShipmentLineInput> Items);
public sealed record DeliverShipment(string OrderVersion, string Version, Guid OperationId);
public sealed record ShipmentLineDto(Guid OrderItemId, int Quantity);
public sealed record ShipmentDto(Guid Id, Guid OrderId, string OrderVersion, string Version, string Carrier,
    string TrackingNumber, ShipmentStatus Status, DateTime DispatchedAtUtc, DateTime? DeliveredAtUtc, List<ShipmentLineDto> Items);

public interface IShippingService
{
    Task<ShipmentDto> DispatchShipmentAsync(Guid orderId, DispatchShipment request, CancellationToken ct);
    Task<ShipmentDto> DeliverShipmentAsync(Guid orderId, Guid shipmentId, DeliverShipment request, CancellationToken ct);
    Task<List<ShipmentDto>> ShipmentsAsync(Guid orderId, bool admin, CancellationToken ct);
}
