using System.Text.Json;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Identity.Users;
using Boilerate.Application.Ordering.Shared;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using Boilerate.Domain.Ordering;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.DataProtection;

namespace Boilerate.Application.Renewals;

public sealed record VerifyRenewalRequest(Guid Id, VerifyRenewal Input) : IRequest<RenewalAdminDto>;

public sealed class VerifyRenewalRequestValidator : CustomValidator<VerifyRenewalRequest>
{
    public VerifyRenewalRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Input).NotNull();
    }
}

public sealed class VerifyRenewalRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work,
    ICurrentUser actor,
    IUserService users,
    TimeProvider clock,
    IDataProtectionProvider protection) : IRequestHandler<VerifyRenewalRequest, RenewalAdminDto>
{
    public async Task<RenewalAdminDto> Handle(VerifyRenewalRequest request, CancellationToken ct)
    {
        Guid actorId = await OrderingAuthHelper.RequirePermissionAsync(actor, users, "Renewal", "Verify", ct);
        var input = request.Input;
        string note = CatalogRules.Required(input.Note, 1200, "Verification note");
        string? evidence = string.IsNullOrWhiteSpace(input.Evidence)
            ? null
            : CatalogRules.Required(input.Evidence, 1800, "Evidence");
        if (input.NewExpiresAtUtc.HasValue && input.NewExpiresAtUtc.Value.Kind != DateTimeKind.Utc)
        {
            throw new DomainException("Verified expiry must be UTC.");
        }

        DateTime? expiry = input.NewExpiresAtUtc.HasValue
            ? new DateTime(input.NewExpiresAtUtc.Value.Ticks - (input.NewExpiresAtUtc.Value.Ticks % 10), DateTimeKind.Utc)
            : null;
        var payload = new { input.AttemptId, input.Applied, NewExpiresAtUtc = expiry, Note = note, Evidence = evidence };
        DateTime now = clock.GetUtcNow().UtcDateTime;

        return await RenewalWorkflowHelper.ChangeRenewalAsync(
            db,
            work,
            request.Id,
            input.Version,
            input.OperationId,
            actorId,
            now,
            input.Applied ? "VerifyApplied" : "VerifyNotApplied",
            payload,
            false,
            async (renewal, order) =>
            {
                string protectedEvidence = RenewalWorkflowHelper.EvidenceProtector(protection, input.AttemptId)
                    .Protect(JsonSerializer.Serialize(new { Note = note, Evidence = evidence }, OrderFulfillmentCoordinator.UnsafeJsonOptions));
                renewal.Verify(input.AttemptId, input.Applied, expiry, actorId, now, protectedEvidence);
                if (order.PaymentStatus == OrderPaymentStatus.Paid &&
                    order.Status is not (OrderStatus.Cancelled or OrderStatus.Expired) &&
                    order.FulfillmentStatus != OrderFulfillmentStatus.NeedsReview)
                {
                    await OrderFulfillmentCoordinator.ReconcileOrderFulfillmentAsync(db, order, ct);
                }
            },
            ct);
    }
}

