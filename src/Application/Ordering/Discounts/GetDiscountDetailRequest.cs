using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Domain.Ordering;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Ordering.Discounts;

public sealed record GetDiscountDetailRequest(Guid Id) : IRequest<DiscountDto>;

public sealed class GetDiscountDetailRequestValidator : CustomValidator<GetDiscountDetailRequest>
{
    public GetDiscountDetailRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public sealed class GetDiscountDetailRequestHandler(IApplicationDbContext db)
    : IRequestHandler<GetDiscountDetailRequest, DiscountDto>
{
    public async Task<DiscountDto> Handle(GetDiscountDetailRequest request, CancellationToken ct)
    {
        var discount = await db.Set<Discount>().AsNoTracking().SingleOrDefaultAsync(d => d.Id == request.Id, ct)
            ?? throw new NotFoundException("Discount not found.");
        int count = await db.Orders.CountAsync(o => o.DiscountId == request.Id && o.DiscountUsageHeld, ct);
        return DiscountMapper.Map(discount, count);
    }
}

