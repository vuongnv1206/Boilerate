using System.Security.Cryptography;
using System.Text;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Ordering.Shared;
using Boilerate.Domain.Renewals;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Renewals;

public sealed record SetRenewalTargetRequest(
    Guid OrderId,
    Guid Id,
    SetRenewalTarget Input) : IRequest<RenewalDto>;

public sealed class SetRenewalTargetRequestValidator : CustomValidator<SetRenewalTargetRequest>
{
    public SetRenewalTargetRequestValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Input).NotNull();
    }
}

public sealed class SetRenewalTargetRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work,
    ICurrentUser actor,
    TimeProvider clock,
    IDataProtectionProvider protection) : IRequestHandler<SetRenewalTargetRequest, RenewalDto>
{
    public async Task<RenewalDto> Handle(SetRenewalTargetRequest request, CancellationToken ct)
    {
        Guid customer = OrderingAuthHelper.RequireCustomer(actor);
        string target = RenewalRequest.NormalizeTarget(request.Input.AccountIdentifier);
        string hash = OrderFulfillmentCoordinator.RenewalHash(new { AccountIdentifier = target });
        DateTime now = clock.GetUtcNow().UtcDateTime;

        return await work.ExecuteAsync(async () =>
        {
            await OrderingAuthHelper.AcquireOrderingGateAsync(db, ct);
            var order = await RenewalWorkflowHelper.LoadRenewalOrderAsync(db, request.OrderId, customer, ct);
            var renewal = await RenewalWorkflowHelper.RenewalRequests(db)
                .SingleOrDefaultAsync(r => r.Id == request.Id && r.OrderId == request.OrderId, ct)
                ?? throw new NotFoundException("Renewal not found.");
            if (!await RenewalWorkflowHelper.ReplayAsync(db, request.Id, request.Input.OperationId, customer, "TargetSet", hash, ct))
            {
                CommerceConcurrency.CheckVersion(request.Input.Version, renewal.Version);
                RenewalWorkflowHelper.RequireRenewalOrder(order);
                string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(target.Normalize(NormalizationForm.FormKC).ToUpperInvariant())));
                string preview = target.Length > 4 ? $"***{target[^4..]}" : "***";
                renewal.SetTarget(RenewalWorkflowHelper.TargetProtector(protection, request.Id).Protect(target), key, preview);
                OrderFulfillmentCoordinator.RecordRenewalOperation(db, renewal, request.Input.OperationId, customer, "TargetSet", hash, now);
            }

            return RenewalWorkflowHelper.MapRenewal(renewal, order.Items.Single(i => i.Id == renewal.OrderItemId));
        }, ct);
    }
}

