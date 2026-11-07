using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Domain.Ordering;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Ordering.Discounts;

public sealed record GetDiscountsRequest(
    int Page = 1,
    int Size = 20,
    string? Search = null,
    bool? IsActive = null) : IRequest<Page<DiscountDto>>;

public sealed class GetDiscountsRequestValidator : CustomValidator<GetDiscountsRequest>
{
    public GetDiscountsRequestValidator()
    {
        RuleFor(x => x.Search).MaximumLength(200);
    }
}

public sealed class GetDiscountsRequestHandler(IApplicationDbContext db)
    : IRequestHandler<GetDiscountsRequest, Page<DiscountDto>>
{
    public async Task<Page<DiscountDto>> Handle(GetDiscountsRequest request, CancellationToken ct)
    {
        CommerceConcurrency.ValidatePage(request.Page, request.Size);
        var query = db.Set<Discount>().AsNoTracking();
        if (request.IsActive.HasValue)
        {
            query = query.Where(d => d.IsActive == request.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            string text = request.Search.Trim().ToUpperInvariant();
            query = query.Where(d => d.Code.Contains(text) || d.Name.ToUpper().Contains(text));
        }

        int total = await query.CountAsync(ct);
        var discounts = await query
            .OrderByDescending(d => d.CreatedOn)
            .ThenBy(d => d.Id)
            .Skip((request.Page - 1) * request.Size)
            .Take(request.Size)
            .ToListAsync(ct);
        var ids = discounts.Select(d => d.Id).ToArray();
        var counts = await db.Orders.AsNoTracking()
            .Where(o => o.DiscountUsageHeld && o.DiscountId.HasValue && ids.Contains(o.DiscountId.Value))
            .GroupBy(o => o.DiscountId!.Value)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, ct);
        return new Page<DiscountDto>(
            discounts.Select(d => DiscountMapper.Map(d, counts.GetValueOrDefault(d.Id))).ToList(),
            total,
            request.Page,
            request.Size);
    }
}

