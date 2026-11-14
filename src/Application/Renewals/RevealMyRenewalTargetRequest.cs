using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Ordering.Shared;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.DataProtection;

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
    public Task<RenewalContentDto> Handle(RevealMyRenewalTargetRequest request, CancellationToken ct) =>
        RenewalWorkflowHelper.RevealTargetForBuyerAsync(db, work, protection, clock.GetUtcNow().UtcDateTime,
            request.OrderId, request.Id, OrderingAuthHelper.RequireCustomer(actor), null, ct);
}
