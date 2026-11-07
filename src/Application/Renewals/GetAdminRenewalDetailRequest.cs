using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Identity.Users;
using Boilerate.Application.Ordering.Shared;
using Boilerate.Domain.Ordering;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Renewals;

public sealed record GetAdminRenewalDetailRequest(Guid Id) : IRequest<RenewalAdminDto>;

public sealed class GetAdminRenewalDetailRequestValidator : CustomValidator<GetAdminRenewalDetailRequest>
{
    public GetAdminRenewalDetailRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public sealed class GetAdminRenewalDetailRequestHandler(
    IApplicationDbContext db,
    ICurrentUser actor,
    IUserService users,
    TimeProvider clock) : IRequestHandler<GetAdminRenewalDetailRequest, RenewalAdminDto>
{
    public async Task<RenewalAdminDto> Handle(GetAdminRenewalDetailRequest request, CancellationToken ct)
    {
        Guid actorId = await OrderingAuthHelper.RequirePermissionAsync(actor, users, "Renewal", "View", ct);
        var renewal = await RenewalWorkflowHelper.RenewalRequests(db)
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == request.Id, ct)
            ?? throw new NotFoundException("Renewal not found.");
        var item = await db.Set<OrderItem>().AsNoTracking().SingleAsync(i => i.Id == renewal.OrderItemId, ct);
        return RenewalWorkflowHelper.MapRenewalAdmin(renewal, item, actorId, clock.GetUtcNow().UtcDateTime, true);
    }
}

