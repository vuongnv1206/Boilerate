using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Contracts;
using Boilerate.Domain.Common.Exceptions;
namespace Boilerate.Domain.Renewals;

public enum RenewalStatus
{
    AwaitingTarget = 1, Ready = 2, Claimed = 3, PendingVerification = 4, Completed = 5
}

public enum RenewalOutcome
{
    Pending = 1, Applied = 2, NotApplied = 3
}

public sealed class RenewalRequest : CommerceEntity, IAggregateRoot
{
    private readonly List<RenewalAttempt> _attempts = [];
    public Guid OrderId { get; private set; }
    public Guid OrderItemId { get; private set; }
    public Guid VariantId { get; private set; }
    public ProductFulfillmentKind FulfillmentKind { get; private set; } = ProductFulfillmentKind.ManualExtension;
    public RenewalStatus Status { get; private set; } = RenewalStatus.AwaitingTarget;
    public string? ProtectedTarget { get; private set; }
    public string? TargetKey { get; private set; }
    public string? TargetPreview { get; private set; }
    public bool TargetLocked { get; private set; }
    public Guid? ClaimedBy { get; private set; }
    public Guid? ClaimToken { get; private set; }
    public DateTime? ClaimUntilUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public DateTime? VerifiedExpiresAtUtc { get; private set; }
    public IReadOnlyCollection<RenewalAttempt> Attempts => _attempts;

    private RenewalRequest()
    {
    }

    public RenewalRequest(Guid orderId, Guid orderItemId, Guid variantId)
    {
        RenewalRules.Id(orderId, "Order");
        RenewalRules.Id(orderItemId, "Order item");
        RenewalRules.Id(variantId, "Variant");
        OrderId = orderId;
        OrderItemId = orderItemId;
        VariantId = variantId;
    }

    public static string NormalizeTarget(string? target)
    {
        if (target is not null && target.Any(char.IsControl))
            throw new DomainException("Account identifiers cannot contain control characters.");
        return CatalogRules.Required(target, 256, "Account identifier");
    }

    public void SetTarget(string protectedTarget, string targetKey, string preview)
    {
        if (TargetLocked || Status is not (RenewalStatus.AwaitingTarget or RenewalStatus.Ready))
            throw new DomainException("The account identifier can only change before work has been claimed or started.");
        var validTarget = CatalogRules.Required(protectedTarget, 4000, "Protected account identifier");
        RenewalRules.Hash(targetKey);
        var validPreview = CatalogRules.Required(preview, 100, "Account preview");
        ProtectedTarget = validTarget;
        TargetKey = targetKey;
        TargetPreview = validPreview;
        Status = RenewalStatus.Ready;
        Touch();
    }

    public Guid Claim(Guid actor, DateTime now, int minutes)
    {
        RenewalRules.Id(actor, "Actor");
        var validNow = RenewalRules.Utc(now);
        if (minutes is < 1 or > 120 || now > DateTime.MaxValue.AddMinutes(-minutes))
            throw new DomainException("A renewal claim must last from 1 to 120 minutes within the supported date range.");
        if (Status != RenewalStatus.Ready && !(Status == RenewalStatus.Claimed && validNow >= ClaimUntilUtc))
            throw new DomainException("Only ready work or an expired claim can be claimed.");
        if (ProtectedTarget is null || TargetKey is null || TargetPreview is null)
            throw new DomainException("The renewal account identifier is required before claiming work.");
        var token = Guid.NewGuid();
        ClaimedBy = actor;
        ClaimToken = token;
        ClaimUntilUtc = validNow.AddMinutes(minutes);
        Status = RenewalStatus.Claimed;
        Touch();
        return token;
    }

    public void Release(Guid actor, Guid token, DateTime now)
    {
        RequireClaim(actor, token, now);
        Status = RenewalStatus.Ready;
        ClearClaim();
        Touch();
    }

    public bool ReleaseExpired(DateTime now)
    {
        var validNow = RenewalRules.Utc(now);
        if (Status != RenewalStatus.Claimed || validNow < ClaimUntilUtc)
            return false;
        Status = RenewalStatus.Ready;
        ClearClaim();
        Touch();
        return true;
    }

    public RenewalAttempt Start(Guid actor, Guid token, Guid operationId, DateTime now)
    {
        RequireClaim(actor, token, now);
        RenewalRules.Id(operationId, "Operation");
        if (_attempts.Any(attempt => attempt.OperationId == operationId || attempt.Outcome == RenewalOutcome.Pending))
            throw new DomainException("A renewal operation cannot be reused or started while an earlier outcome is unresolved.");
        var attempt = new RenewalAttempt(Id, operationId, actor, now);
        _attempts.Add(attempt);
        TargetLocked = true;
        Status = RenewalStatus.PendingVerification;
        ClaimToken = null;
        ClaimUntilUtc = null;
        Touch();
        return attempt;
    }

    public void Verify(Guid attemptId, bool applied, DateTime? expiry, Guid actor, DateTime now, string protectedEvidence)
    {
        RenewalRules.Id(attemptId, "Attempt");
        if (Status != RenewalStatus.PendingVerification)
            throw new DomainException("Only a renewal awaiting verification can be resolved.");
        var attempt = _attempts.SingleOrDefault(item => item.Id == attemptId)
            ?? throw new DomainException("The renewal attempt does not belong to this request.");
        attempt.Resolve(applied, expiry, actor, now, protectedEvidence);
        Status = applied ? RenewalStatus.Completed : RenewalStatus.Ready;
        CompletedAtUtc = applied ? attempt.VerifiedAtUtc : null;
        VerifiedExpiresAtUtc = applied ? attempt.NewExpiresAtUtc : null;
        ClearClaim();
        Touch();
    }

    private void RequireClaim(Guid actor, Guid token, DateTime now)
    {
        RenewalRules.Id(actor, "Actor");
        RenewalRules.Id(token, "Claim token");
        var validNow = RenewalRules.Utc(now);
        if (Status != RenewalStatus.Claimed || ClaimedBy != actor || ClaimToken != token || validNow >= ClaimUntilUtc)
            throw new DomainException("A valid, unexpired claim belonging to the current actor is required.");
    }

    private void ClearClaim()
    {
        ClaimedBy = null;
        ClaimToken = null;
        ClaimUntilUtc = null;
    }
}

internal static class RenewalRules
{
    public static void Id(Guid value, string name)
    {
        if (value == Guid.Empty)
            throw new DomainException($"{name} ID is required.");
    }

    public static void Hash(string? value)
    {
        if (value is null || value.Length != 64 || value.Any(c => !char.IsAsciiHexDigit(c)))
            throw new DomainException("A SHA-256 hexadecimal value is required.");
    }

    public static DateTime Utc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc)
            throw new DomainException("Renewal timestamps must be UTC.");
        return new DateTime(value.Ticks - value.Ticks % 10, DateTimeKind.Utc);
    }
}
