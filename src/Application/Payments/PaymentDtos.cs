using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Ordering.Shared;
using Boilerate.Domain.Auditing;
using Boilerate.Domain.Ordering;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Payments;

public sealed record ConfirmPayment(
    string Version,
    Guid OperationId,
    string Reference,
    decimal Amount,
    string Currency,
    DateTime ReceivedAtUtc,
    string Source = "ManualBankTransfer");

public sealed record RetryFulfillment(string Version);

public sealed record DigitalDeliveryDto(Guid Id, Guid OrderItemId, int Sequence, string Content);

internal static class DigitalDeliveryReader
{
    internal static Task<List<DigitalDeliveryDto>> ReadDeliveriesAsync(
        IApplicationDbContext db,
        ICommerceUnitOfWork work,
        IDataProtectionProvider protection,
        DateTime nowUtc,
        Guid orderId,
        Guid? customer,
        string? verifiedEmail,
        CancellationToken ct) =>
        work.ExecuteAsync(async () =>
        {
            var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(
                o => o.Id == orderId &&
                    (customer.HasValue
                        ? !o.IsGuestOrder && o.CustomerId == customer.Value
                        : o.IsGuestOrder && o.NormalizedEmail == verifiedEmail),
                ct) ?? throw new NotFoundException("Order not found.");
            if (order.PaymentStatus != OrderPaymentStatus.Paid)
            {
                throw new ConflictException("Digital content is available only after payment confirmation.");
            }

            var deliveries = await db.DigitalDeliveries.AsNoTracking()
                .Where(d => d.OrderId == orderId)
                .OrderBy(d => d.OrderItemId)
                .ThenBy(d => d.Sequence)
                .ToListAsync(ct);
            var result = new List<DigitalDeliveryDto>();
            foreach (var delivery in deliveries)
            {
                result.Add(new DigitalDeliveryDto(
                    delivery.Id,
                    delivery.OrderItemId,
                    delivery.Sequence,
                    OrderFulfillmentCoordinator.DeliveryProtector(protection, delivery.Id).Unprotect(delivery.ProtectedContent)));
                db.Trails.Add(new Trail
                {
                    UserId = customer ?? Guid.Empty,
                    TableName = "DigitalDeliveries",
                    PrimaryKey = delivery.Id.ToString(),
                    Type = TrailType.Read,
                    DateTime = nowUtc,
                    NewValues = "{\"Action\":\"Reveal\"}",
                });
            }

            return result;
        }, ct);
}

