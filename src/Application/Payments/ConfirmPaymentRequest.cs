using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Identity.Users;
using Boilerate.Application.Ordering.Shared;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using Boilerate.Domain.Ordering;
using Boilerate.Domain.Payments;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Payments;

public sealed record ConfirmPaymentRequest(Guid OrderId, ConfirmPayment Input) : IRequest<OrderDto>;

public sealed class ConfirmPaymentRequestValidator : CustomValidator<ConfirmPaymentRequest>
{
    public ConfirmPaymentRequestValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Input).NotNull();
    }
}

public sealed class ConfirmPaymentRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work,
    ICurrentUser actor,
    IUserService users,
    TimeProvider clock,
    IDataProtectionProvider protection) : IRequestHandler<ConfirmPaymentRequest, OrderDto>
{
    public async Task<OrderDto> Handle(ConfirmPaymentRequest request, CancellationToken ct)
    {
        Guid actorId = await OrderingAuthHelper.RequirePermissionAsync(actor, users, "Payment", "Confirm", ct);
        var r = request.Input;
        string reference = CatalogRules.Code(r.Reference).ToUpperInvariant();
        string currency = CatalogRules.Required(r.Currency, 3, "Currency").ToUpperInvariant();
        if (r.Source is not "ManualBankTransfer" and not "ManualCOD")
        {
            throw new DomainException("Receipt source must be ManualBankTransfer or ManualCOD.");
        }

        CatalogRules.Money(r.Amount, null);
        string hash = PaymentHash(request.OrderId, r, reference, currency);
        DateTime now = clock.GetUtcNow().UtcDateTime;

        return await work.ExecuteAsync(async () =>
        {
            await OrderingAuthHelper.AcquireOrderingGateAsync(db, ct);
            var prior = await db.PaymentReceipts.SingleOrDefaultAsync(p => p.OperationId == r.OperationId, ct);
            if (prior is not null)
            {
                if (prior.OrderId != request.OrderId || prior.RequestHash != hash)
                {
                    throw new ConflictException("OperationId was already used with different payment content.");
                }

                return OrderFulfillmentCoordinator.MapOrder(
                    await OrderStockCoordinator.Orders(db).SingleAsync(o => o.Id == request.OrderId, ct));
            }

            if (await db.PaymentReceipts.AnyAsync(p => p.Source == r.Source && p.NormalizedReference == reference, ct))
            {
                throw new ConflictException("This payment reference has already been recorded for the source.");
            }

            var order = await OrderStockCoordinator.Orders(db).SingleOrDefaultAsync(o => o.Id == request.OrderId, ct)
                ?? throw new NotFoundException("Order not found.");
            CommerceConcurrency.CheckVersion(r.Version, order.Version);
            if (order.PaymentStatus == OrderPaymentStatus.Paid)
            {
                throw new ConflictException("The order has already been paid.");
            }

            if (r.Amount != order.Total || currency != order.Currency)
            {
                throw new DomainException("Confirm the exact full order total and currency; partial or excess receipts require separate review.");
            }

            if (r.Source == "ManualCOD" &&
                (order.PaymentMethod != OrderPaymentMethod.CashOnDelivery ||
                 !await OrderFulfillmentCoordinator.AllPhysicalDeliveredAsync(db, order, ct)))
            {
                throw new ConflictException("COD receipts require a COD order with all physical quantities delivered.");
            }

            var receipt = new PaymentReceipt(order.Id, r.OperationId, hash, r.Reference, r.Amount, currency, r.ReceivedAtUtc, now, r.Source);
            db.PaymentReceipts.Add(receipt);
            order.RecordPayment(r.ReceivedAtUtc);
            if (order.Status is not (OrderStatus.Cancelled or OrderStatus.Closed) &&
                !await OrderStockCoordinator.HoldDiscountUsageAsync(db, order, ct))
            {
                order.MarkFulfillmentReview();
                return OrderFulfillmentCoordinator.MapOrder(order);
            }

            await OrderFulfillmentCoordinator.EnsureRenewalRequestsAsync(db, order, actorId, now, ct);
            if (order.PhysicalDispatchStarted)
            {
                await OrderFulfillmentCoordinator.ReconcileOrderFulfillmentAsync(db, order, ct);
            }
            else if (order.Status is not (OrderStatus.Cancelled or OrderStatus.Closed))
            {
                await OrderFulfillmentCoordinator.FulfillPaidOrderAsync(db, protection, order, actorId, now, ct);
            }

            return OrderFulfillmentCoordinator.MapOrder(order);
        }, ct);
    }

    private static string PaymentHash(Guid orderId, ConfirmPayment r, string reference, string currency)
    {
        string payload = r.Source == "ManualBankTransfer"
            ? JsonSerializer.Serialize(new
            {
                OrderId = orderId,
                Reference = reference,
                Amount = r.Amount.ToString("0.##", CultureInfo.InvariantCulture),
                Currency = currency,
                r.ReceivedAtUtc,
            })
            : JsonSerializer.Serialize(new
            {
                OrderId = orderId,
                Reference = reference,
                Amount = r.Amount.ToString("0.##", CultureInfo.InvariantCulture),
                Currency = currency,
                r.ReceivedAtUtc,
                r.Source,
            });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }
}

