using System.Globalization;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Identity.Users;
using Boilerate.Application.Ordering.Shared;
using Boilerate.Application.Renewals;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using Boilerate.Domain.Ordering;
using Boilerate.Domain.Payments;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.AfterSales;

public sealed record ConfirmRefundRequest(
    Guid OrderId,
    ConfirmRefund Input) : IRequest<RefundResult>;

public sealed class ConfirmRefundRequestValidator : CustomValidator<ConfirmRefundRequest>
{
    public ConfirmRefundRequestValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Input).NotNull();
    }
}

public sealed class ConfirmRefundRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work,
    ICurrentUser actor,
    IUserService users,
    TimeProvider clock) : IRequestHandler<ConfirmRefundRequest, RefundResult>
{
    public async Task<RefundResult> Handle(ConfirmRefundRequest request, CancellationToken ct)
    {
        Guid actorId = await OrderingAuthHelper.RequirePermissionAsync(actor, users, "Refund", "Confirm", ct);
        var input = request.Input;
        if (input.OperationId == Guid.Empty)
        {
            throw new DomainException("OperationId is required.");
        }

        string reference = CatalogRules.Code(input.Reference);
        string normalized = reference.ToUpperInvariant();
        string reason = CatalogRules.Required(input.Reason, 1000, "Reason");
        string currency = CatalogRules.Required(input.Currency, 3, "Currency").ToUpperInvariant();
        string source = CatalogRules.Required(input.Source, 32, "Source");
        CatalogRules.Money(input.Amount, null);
        if (input.Amount <= 0 || currency != "VND" || source is not ("ManualBankTransfer" or "ManualCash"))
        {
            throw new DomainException("Refunds require a positive whole VND amount and a supported manual source.");
        }

        DateTime refundedAt = AfterSalesHelper.NormalizeUtcTime(input.RefundedAtUtc);
        DateTime now = clock.GetUtcNow().UtcDateTime;
        string hash = AfterSalesHelper.Hash(new
        {
            request.OrderId,
            ActorId = actorId,
            Reference = normalized,
            Reason = reason,
            Currency = currency,
            Source = source,
            RefundedAtUtc = refundedAt,
            Amount = input.Amount.ToString("0.##", CultureInfo.InvariantCulture),
        });

        return await work.ExecuteAsync(async () =>
        {
            await OrderingAuthHelper.AcquireOrderingGateAsync(db, ct);
            var prior = await db.Refunds.SingleOrDefaultAsync(r => r.OperationId == input.OperationId, ct);
            if (prior is not null)
            {
                if (prior.OrderId != request.OrderId || prior.ActorId != actorId || prior.RequestHash != hash)
                {
                    throw new ConflictException("Refund OperationId was already used for another actor, order or content.");
                }

                var current = await db.Orders.SingleAsync(o => o.Id == request.OrderId, ct);
                return new RefundResult(AfterSalesHelper.MapRefund(prior), current.Version, current.Status == OrderStatus.Closed);
            }

            var order = await RenewalWorkflowHelper.LoadRenewalOrderAsync(db, request.OrderId, null, ct);
            CommerceConcurrency.CheckVersion(input.OrderVersion, order.Version);
            var receipt = await db.PaymentReceipts.SingleOrDefaultAsync(p => p.OrderId == request.OrderId, ct)
                ?? throw new ConflictException("A confirmed receipt is required before recording a refund.");
            if (order.PaymentStatus != OrderPaymentStatus.Paid)
            {
                throw new ConflictException("Only received money can be refunded.");
            }

            if (await db.Refunds.AnyAsync(r => r.Source == source && r.NormalizedReference == normalized, ct))
            {
                throw new ConflictException("This refund reference has already been recorded.");
            }

            decimal total = await db.Refunds.Where(r => r.ReceiptId == receipt.Id).SumAsync(r => (decimal?)r.Amount, ct) ?? 0;
            var refund = new Refund(
                receipt.Id,
                request.OrderId,
                input.OperationId,
                hash,
                reference,
                input.Amount,
                currency,
                refundedAt,
                CommerceConcurrency.UtcTimestamp(receipt.ReceivedAtUtc),
                now,
                reason,
                source,
                actorId);
            if (currency != receipt.Currency || refund.Amount > receipt.Amount - total)
            {
                throw new ConflictException("Refunds cannot exceed the receipt's remaining amount or change currency.");
            }

            db.Refunds.Add(refund);
            if (total + refund.Amount == receipt.Amount)
            {
                await OrderFulfillmentCoordinator.CloseAfterSalesAsync(db, order, actorId, now, ct);
            }

            order.Touch();
            return new RefundResult(AfterSalesHelper.MapRefund(refund), order.Version, order.Status == OrderStatus.Closed);
        }, ct);
    }
}

