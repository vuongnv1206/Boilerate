using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Ordering.Shared;
using FluentValidation;
using MediatR;

namespace Boilerate.Application.Renewals;

public sealed record GetMyOrderRenewalsRequest(Guid OrderId) : IRequest<List<RenewalDto>>;

public sealed class GetMyOrderRenewalsRequestValidator : CustomValidator<GetMyOrderRenewalsRequest>
{
    public GetMyOrderRenewalsRequestValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
    }
}

public sealed class GetMyOrderRenewalsRequestHandler(
    IApplicationDbContext db,
    ICurrentUser actor) : IRequestHandler<GetMyOrderRenewalsRequest, List<RenewalDto>>
{
    public Task<List<RenewalDto>> Handle(GetMyOrderRenewalsRequest request, CancellationToken ct) =>
        RenewalWorkflowHelper.ListForBuyerAsync(db, request.OrderId, OrderingAuthHelper.RequireCustomer(actor), null, ct);
}
