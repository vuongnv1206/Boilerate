using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Identity.Users;
using Boilerate.Application.Ordering.Shared;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Renewals;

public sealed record ProvisionOrderRenewalsRequest(
    Guid OrderId,
    ProvisionRenewals Input) : IRequest<List<RenewalDto>>;

public sealed class ProvisionOrderRenewalsRequestValidator : CustomValidator<ProvisionOrderRenewalsRequest>
{
    public ProvisionOrderRenewalsRequestValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Input).NotNull();
    }
}

public sealed class ProvisionOrderRenewalsRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work,
    ICurrentUser actor,
    IUserService users,
    TimeProvider clock) : IRequestHandler<ProvisionOrderRenewalsRequest, List<RenewalDto>>
{
    public async Task<List<RenewalDto>> Handle(ProvisionOrderRenewalsRequest request, CancellationToken ct)
    {
        Guid actorId = await OrderingAuthHelper.RequirePermissionAsync(actor, users, "Renewal", "Process", ct);
        DateTime now = clock.GetUtcNow().UtcDateTime;
        return await work.ExecuteAsync(async () =>
        {
            await OrderingAuthHelper.AcquireOrderingGateAsync(db, ct);
            var order = await RenewalWorkflowHelper.LoadRenewalOrderAsync(db, request.OrderId, null, ct);
            CommerceConcurrency.CheckVersion(request.Input.OrderVersion, order.Version);
            RenewalWorkflowHelper.RequireRenewalOrder(order);
            await OrderFulfillmentCoordinator.EnsureRenewalRequestsAsync(db, order, actorId, now, ct);
            await db.SaveChangesAsync(ct);
            var requests = await db.RenewalRequests
                .Where(r => r.OrderId == request.OrderId)
                .OrderBy(r => r.Id)
                .ToListAsync(ct);
            return requests.Select(r => RenewalWorkflowHelper.MapRenewal(r, order.Items.Single(i => i.Id == r.OrderItemId))).ToList();
        }, ct);
    }
}

