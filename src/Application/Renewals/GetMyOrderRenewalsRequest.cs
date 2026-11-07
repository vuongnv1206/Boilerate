using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Ordering.Shared;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

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
    public async Task<List<RenewalDto>> Handle(GetMyOrderRenewalsRequest request, CancellationToken ct)
    {
        Guid customer = OrderingAuthHelper.RequireCustomer(actor);
        var order = await RenewalWorkflowHelper.LoadRenewalOrderAsync(db, request.OrderId, customer, ct);
        var requests = await db.RenewalRequests.AsNoTracking()
            .Where(r => r.OrderId == request.OrderId)
            .OrderBy(r => r.Id)
            .ToListAsync(ct);
        return requests.Select(r => RenewalWorkflowHelper.MapRenewal(r, order.Items.Single(i => i.Id == r.OrderItemId))).ToList();
    }
}

