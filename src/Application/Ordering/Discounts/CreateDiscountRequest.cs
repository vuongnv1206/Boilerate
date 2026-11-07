using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Ordering.Shared;
using Boilerate.Domain.Ordering;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Ordering.Discounts;

public sealed record CreateDiscountRequest(SaveDiscount Input) : IRequest<DiscountDto>;

public sealed class CreateDiscountRequestValidator : CustomValidator<CreateDiscountRequest>
{
    public CreateDiscountRequestValidator()
    {
        RuleFor(x => x.Input).NotNull();
    }
}

public sealed class CreateDiscountRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work) : IRequestHandler<CreateDiscountRequest, DiscountDto>
{
    public Task<DiscountDto> Handle(CreateDiscountRequest request, CancellationToken ct) =>
        work.ExecuteAsync(async () =>
        {
            var r = request.Input;
            await OrderingAuthHelper.AcquireOrderingGateAsync(db, ct, "Apply ordering migrations before changing discounts.");
            var discount = new Discount(r.Code, r.Name, r.Kind, r.Value, r.MinimumSubtotal, r.StartsAtUtc, r.EndsAtUtc, r.UsageLimit, r.IsActive);
            if (await db.Set<Discount>().AnyAsync(d => d.Code == discount.Code, ct))
            {
                throw new ConflictException("Discount code already exists.");
            }

            db.Set<Discount>().Add(discount);
            return DiscountMapper.Map(discount, 0);
        }, ct);
}

