using Boilerate.Domain.Common.Exceptions;
using Boilerate.Domain.Ordering;
using Boilerate.Domain.Renewals;

namespace Boilerate.Infrastructure.Commerce;

/// <summary>Read-only queues shared by overview counts and their destination lists.</summary>
internal static class OverviewQueues
{
    internal static IQueryable<Order> Orders(IQueryable<Order> query, string? queue, DateTime now) => queue switch
    {
        null or "" => query,
        "awaiting-payment" => query.Where(o => o.Status == OrderStatus.PendingPayment &&
            o.PaymentStatus == OrderPaymentStatus.Unpaid && (!o.ExpiresAt.HasValue || o.ExpiresAt > now)),
        "awaiting-processing" => query.Where(o => o.Status == OrderStatus.AwaitingProcessing &&
            (o.FulfillmentStatus == OrderFulfillmentStatus.Pending || o.FulfillmentStatus == OrderFulfillmentStatus.InProgress)),
        "needs-review" => query.Where(o => o.Status != OrderStatus.Closed &&
            o.PaymentStatus == OrderPaymentStatus.Paid && o.FulfillmentStatus == OrderFulfillmentStatus.NeedsReview),
        _ => throw new DomainException("Invalid order queue.")
    };

    internal static IQueryable<RenewalRequest> Renewals(IQueryable<RenewalRequest> query, IQueryable<Order> orders, string? queue) => queue switch
    {
        null or "" => query,
        "pending-verification" => query.Where(r => r.Status == RenewalStatus.PendingVerification),
        "awaiting-work" => query.Where(r => (r.Status == RenewalStatus.Ready || r.Status == RenewalStatus.Claimed) &&
            orders.Any(o => o.Id == r.OrderId && o.PaymentStatus == OrderPaymentStatus.Paid &&
                o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.Expired && o.Status != OrderStatus.Closed &&
                o.FulfillmentStatus != OrderFulfillmentStatus.NeedsReview)),
        _ => throw new DomainException("Invalid renewal queue.")
    };
}
