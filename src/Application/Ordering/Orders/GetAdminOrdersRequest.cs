using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Identity.Users;
using Boilerate.Application.Ordering.Shared;
using Boilerate.Application.Overview;
using Boilerate.Domain.Common.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Ordering.Orders;

public sealed record GetAdminOrdersRequest(AdminOrderQuery Query) : IRequest<Page<OrderSummary>>;

public sealed class GetAdminOrdersRequestValidator : CustomValidator<GetAdminOrdersRequest>
{
    public GetAdminOrdersRequestValidator()
    {
        RuleFor(x => x.Query).NotNull();
    }
}

public sealed class GetAdminOrdersRequestHandler(
    IApplicationDbContext db,
    ICurrentUser actor,
    IUserService users,
    TimeProvider clock) : IRequestHandler<GetAdminOrdersRequest, Page<OrderSummary>>
{
    public async Task<Page<OrderSummary>> Handle(GetAdminOrdersRequest request, CancellationToken ct)
    {
        await OrderingAuthHelper.RequirePermissionAsync(actor, users, "Order", "View", ct);
        var q = request.Query;
        CommerceConcurrency.ValidatePage(q.Page, q.Size);
        if (q.CreatedFrom.HasValue && q.CreatedTo.HasValue && q.CreatedFrom > q.CreatedTo)
        {
            throw new DomainException("CreatedFrom must be earlier than CreatedTo.");
        }

        DateTime now = clock.GetUtcNow().UtcDateTime;
        var query = db.Orders.AsNoTracking();
        query = OverviewQueueFilters.Orders(query, q.Queue, now);
        if (q.Status.HasValue)
        {
            query = query.Where(o => o.Status == q.Status.Value);
        }

        if (q.PaymentStatus.HasValue)
        {
            query = query.Where(o => o.PaymentStatus == q.PaymentStatus.Value);
        }

        if (q.FulfillmentStatus.HasValue)
        {
            query = query.Where(o => o.FulfillmentStatus == q.FulfillmentStatus.Value);
        }

        if (q.CreatedFrom.HasValue)
        {
            query = query.Where(o => o.CreatedOn >= q.CreatedFrom.Value);
        }

        if (q.CreatedTo.HasValue)
        {
            query = query.Where(o => o.CreatedOn <= q.CreatedTo.Value);
        }

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            string search = q.Search.Trim().ToUpperInvariant();
            query = query.Where(o =>
                o.Number.ToUpper().Contains(search) ||
                o.Email.ToUpper().Contains(search) ||
                o.Phone.ToUpper().Contains(search));
        }

        int total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(o => o.CreatedOn)
            .ThenByDescending(o => o.Id)
            .Skip((q.Page - 1) * q.Size)
            .Take(q.Size)
            .Select(o => new OrderSummary(
                o.Id,
                o.Number,
                o.Status,
                o.PaymentMethod,
                o.Total,
                o.Currency,
                o.CreatedOn,
                o.ExpiresAt,
                o.PaymentStatus,
                o.FulfillmentStatus,
                o.PaidAtUtc,
                o.RecipientName,
                o.Email,
                o.Phone))
            .ToListAsync(ct);
        items = items.Select(o => o with
        {
            CreatedOn = CommerceConcurrency.UtcTimestamp(o.CreatedOn),
            ExpiresAt = CommerceConcurrency.UtcTimestamp(o.ExpiresAt),
            PaidAtUtc = CommerceConcurrency.UtcTimestamp(o.PaidAtUtc),
        }).ToList();
        return new Page<OrderSummary>(items, total, q.Page, q.Size);
    }
}

