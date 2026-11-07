using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Ordering.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Storefront.Orders;

public sealed record GetGuestOrdersRequest(
    string VerifiedEmail,
    int Page = 1,
    int Size = 20) : IRequest<Page<OrderSummary>>;

public sealed class GetGuestOrdersRequestValidator : CustomValidator<GetGuestOrdersRequest>
{
}

public sealed class GetGuestOrdersRequestHandler(IApplicationDbContext db)
    : IRequestHandler<GetGuestOrdersRequest, Page<OrderSummary>>
{
    public async Task<Page<OrderSummary>> Handle(GetGuestOrdersRequest request, CancellationToken ct)
    {
        string email = OrderingAuthHelper.NormalizeEmail(request.VerifiedEmail);
        CommerceConcurrency.ValidatePage(request.Page, request.Size);
        var query = db.Orders.AsNoTracking().Where(o => o.IsGuestOrder && o.NormalizedEmail == email);
        int total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(o => o.CreatedOn)
            .ThenByDescending(o => o.Id)
            .Skip((request.Page - 1) * request.Size)
            .Take(request.Size)
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
        return new Page<OrderSummary>(items, total, request.Page, request.Size);
    }
}

