using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Ordering.Shared;
using Boilerate.Domain.Ordering;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Renewals;

public sealed record RevealMyRenewalTargetRequest(Guid OrderId, Guid Id) : IRequest<RenewalContentDto>;

public sealed class RevealMyRenewalTargetRequestValidator : CustomValidator<RevealMyRenewalTargetRequest>
{
    public RevealMyRenewalTargetRequestValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Id).NotEmpty();
    }
}

public sealed class RevealMyRenewalTargetRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work,
    ICurrentUser actor,
    TimeProvider clock,
    IDataProtectionProvider protection) : IRequestHandler<RevealMyRenewalTargetRequest, RenewalContentDto>
{
    public async Task<RenewalContentDto> Handle(RevealMyRenewalTargetRequest request, CancellationToken ct)
    {
        Guid customer = OrderingAuthHelper.RequireCustomer(actor);
        DateTime now = clock.GetUtcNow().UtcDateTime;
        return await work.ExecuteAsync(async () =>
        {
            if ((await RenewalWorkflowHelper.LoadRenewalOrderAsync(db, request.OrderId, customer, ct)).PaymentStatus != OrderPaymentStatus.Paid)
            {
                throw new ConflictException("Renewal history is available after payment.");
            }

            var renewal = await db.RenewalRequests
                .SingleOrDefaultAsync(r => r.Id == request.Id && r.OrderId == request.OrderId, ct)
                ?? throw new NotFoundException("Renewal not found.");
            return RenewalWorkflowHelper.RevealTarget(db, protection, customer, now, renewal);
        }, ct);
    }
}

