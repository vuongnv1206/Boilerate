using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Contracts;
using Boilerate.Domain.Common.Exceptions;
namespace Boilerate.Domain.Renewals;

public sealed class RenewalAttempt : BaseEntity
{
    public Guid RequestId { get; private set; }
    public Guid OperationId { get; private set; }
    public Guid StartedBy { get; private set; }
    public DateTime StartedAtUtc { get; private set; }
    public RenewalOutcome Outcome { get; private set; } = RenewalOutcome.Pending;
    public Guid? VerifiedBy { get; private set; }
    public DateTime? VerifiedAtUtc { get; private set; }
    public DateTime? NewExpiresAtUtc { get; private set; }
    public string? ProtectedEvidence { get; private set; }

    private RenewalAttempt()
    {
    }

    public RenewalAttempt(Guid requestId, Guid operationId, Guid actor, DateTime now)
    {
        RenewalRules.Id(requestId, "Request");
        RenewalRules.Id(operationId, "Operation");
        RenewalRules.Id(actor, "Actor");
        var validNow = RenewalRules.Utc(now);
        RequestId = requestId;
        OperationId = operationId;
        StartedBy = actor;
        StartedAtUtc = validNow;
    }

    internal void Resolve(bool applied, DateTime? expiry, Guid actor, DateTime now, string protectedEvidence)
    {
        if (Outcome != RenewalOutcome.Pending)
            throw new DomainException("A verified renewal attempt is immutable.");
        RenewalRules.Id(actor, "Verifier");
        var validNow = RenewalRules.Utc(now);
        if (validNow < StartedAtUtc)
            throw new DomainException("Verification cannot precede the start of the renewal attempt.");
        DateTime? validExpiry = expiry.HasValue ? RenewalRules.Utc(expiry.Value) : null;
        if ((applied && (!validExpiry.HasValue || validExpiry <= validNow)) || (!applied && validExpiry.HasValue))
            throw new DomainException("Applied renewals require a verified future expiry; unapplied renewals must not set an expiry.");
        var validEvidence = CatalogRules.Required(protectedEvidence, 32000, "Protected verification evidence");
        Outcome = applied ? RenewalOutcome.Applied : RenewalOutcome.NotApplied;
        VerifiedBy = actor;
        VerifiedAtUtc = validNow;
        NewExpiresAtUtc = validExpiry;
        ProtectedEvidence = validEvidence;
    }
}
