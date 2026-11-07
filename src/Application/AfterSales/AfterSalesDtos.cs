using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Ordering.Shared;
using Boilerate.Application.Renewals;
using Boilerate.Domain.Common.Exceptions;
using Boilerate.Domain.Ordering;
using Boilerate.Domain.Payments;
using Boilerate.Domain.Shipping;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.AfterSales;

public sealed record ConfirmRefund(
    string OrderVersion,
    Guid OperationId,
    string Reference,
    decimal Amount,
    string Currency,
    DateTime RefundedAtUtc,
    string Reason,
    string Source = "ManualBankTransfer");

public sealed record ReturnLineInput(Guid ShipmentId, Guid OrderItemId, int Quantity, int RestockQuantity);

public sealed record ReceiveReturn(
    string OrderVersion,
    Guid OperationId,
    string Reference,
    string Reason,
    List<ReturnLineInput> Items,
    bool CloseRemainingCod = false);

public sealed record RefundDto(
    Guid Id,
    Guid ReceiptId,
    string Reference,
    string Source,
    decimal Amount,
    string Currency,
    string Reason,
    DateTime RefundedAtUtc);

public sealed record ReturnLineDto(Guid ShipmentId, Guid OrderItemId, int Quantity, int RestockQuantity);

public sealed record PhysicalReturnDto(
    Guid Id,
    string Reference,
    string Reason,
    DateTime ReceivedAtUtc,
    bool CloseRemainingCod,
    List<ReturnLineDto> Items);

public sealed record RefundResult(RefundDto Refund, string OrderVersion, bool Closed);

public sealed record ReturnResult(PhysicalReturnDto Return, string OrderVersion, bool Closed);

public sealed record AfterSalesDto(
    Guid OrderId,
    string OrderVersion,
    bool Closed,
    decimal ReceivedAmount,
    decimal RefundedAmount,
    decimal RefundableAmount,
    List<RefundDto> Refunds,
    List<PhysicalReturnDto> Returns);

internal static class AfterSalesHelper
{
    internal static string Hash(object payload) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload))));

    internal static DateTime NormalizeUtcTime(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new DomainException("After-sales timestamps must be UTC.");
        }

        return new DateTime(value.Ticks - (value.Ticks % 10), DateTimeKind.Utc);
    }

    internal static RefundDto MapRefund(Refund r) =>
        new(
            r.Id,
            r.ReceiptId,
            r.Reference,
            r.Source,
            r.Amount,
            r.Currency,
            r.Reason,
            CommerceConcurrency.UtcTimestamp(r.RefundedAtUtc));

    internal static PhysicalReturnDto MapReturn(PhysicalReturn r) =>
        new(
            r.Id,
            r.Reference,
            r.Reason,
            CommerceConcurrency.UtcTimestamp(r.ReceivedAtUtc),
            r.CloseRemainingCod,
            r.Items
                .OrderBy(i => i.ShipmentId)
                .ThenBy(i => i.OrderItemId)
                .Select(i => new ReturnLineDto(i.ShipmentId, i.OrderItemId, i.Quantity, i.RestockQuantity))
                .ToList());

    internal static Task<AfterSalesDto> ReadSummaryAsync(
        IApplicationDbContext db,
        ICommerceUnitOfWork work,
        Guid orderId,
        Guid? ownerId,
        CancellationToken ct) =>
        work.ExecuteAsync(async () =>
        {
            await OrderingAuthHelper.AcquireOrderingGateAsync(db, ct);
            var order = await RenewalWorkflowHelper.LoadRenewalOrderAsync(db, orderId, ownerId, ct);
            decimal received = await db.PaymentReceipts
                .Where(p => p.OrderId == orderId)
                .Select(p => (decimal?)p.Amount)
                .SingleOrDefaultAsync(ct) ?? 0;
            var refunds = await db.Refunds.AsNoTracking()
                .Where(r => r.OrderId == orderId)
                .OrderBy(r => r.RefundedAtUtc)
                .ThenBy(r => r.Id)
                .ToListAsync(ct);
            var returns = await db.PhysicalReturns.AsNoTracking()
                .Include(r => r.Items)
                .Where(r => r.OrderId == orderId)
                .OrderBy(r => r.ReceivedAtUtc)
                .ThenBy(r => r.Id)
                .ToListAsync(ct);
            decimal refunded = refunds.Sum(r => r.Amount);
            return new AfterSalesDto(
                orderId,
                order.Version,
                order.Status == OrderStatus.Closed,
                received,
                refunded,
                received - refunded,
                refunds.Select(MapRefund).ToList(),
                returns.Select(MapReturn).ToList());
        }, ct);
}

