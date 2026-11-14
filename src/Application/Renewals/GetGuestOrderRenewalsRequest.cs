using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using FluentValidation;
using MediatR;

namespace Boilerate.Application.Renewals;

/// <summary>Renewals of a guest order, for the guest who verified the order's email.</summary>
public sealed record GetGuestOrderRenewalsRequest(Guid OrderId, string VerifiedEmail) : IRequest<List<RenewalDto>>;

public sealed class GetGuestOrderRenewalsRequestValidator : CustomValidator<GetGuestOrderRenewalsRequest>
{
    public GetGuestOrderRenewalsRequestValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
    }
}

public sealed class GetGuestOrderRenewalsRequestHandler(
    IApplicationDbContext db) : IRequestHandler<GetGuestOrderRenewalsRequest, List<RenewalDto>>
{
    public Task<List<RenewalDto>> Handle(GetGuestOrderRenewalsRequest request, CancellationToken ct) =>
        RenewalWorkflowHelper.ListForBuyerAsync(db, request.OrderId, null, request.VerifiedEmail, ct);
}
