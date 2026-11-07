using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Ordering.Shared;
using Boilerate.Domain.Ordering;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Ordering.Discounts;

public sealed record UpdateDiscountRequest(Guid Id, SaveDiscount Input) : IRequest<DiscountDto>;

public sealed class UpdateDiscountRequestValidator : CustomValidator<UpdateDiscountRequest>
{
    public UpdateDiscountRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Input).NotNull();
    }
}

public sealed class UpdateDiscountRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work) : IRequestHandler<UpdateDiscountRequest, DiscountDto>
{
    public Task<DiscountDto> Handle(UpdateDiscountRequest request, CancellationToken ct) =>
        work.ExecuteAsync(async () =>
        {
            var r = request.Input;
            await OrderingAuthHelper.AcquireOrderingGateAsync(db, ct, "Apply ordering migrations before changing discounts.");
            var discount = await db.Set<Discount>().SingleOrDefaultAsync(d => d.Id == request.Id, ct)
                ?? throw new NotFoundException("Discount not found.");
            CommerceConcurrency.CheckVersion(r.Version, discount.Version);
            int count = await db.Orders.CountAsync(o => o.DiscountId == request.Id && o.DiscountUsageHeld, ct);
            if (r.UsageLimit < count)
            {
                throw new ConflictException("Usage limit cannot be below the current usage count.");
            }

            discount.Update(r.Code, r.Name, r.Kind, r.Value, r.MinimumSubtotal, r.StartsAtUtc, r.EndsAtUtc, r.UsageLimit, r.IsActive);
            if (await db.Set<Discount>().AnyAsync(d => d.Id != request.Id && d.Code == discount.Code, ct))
            {
                throw new ConflictException("Discount code already exists.");
            }

            return DiscountMapper.Map(discount, count);
        }, ct);
}

