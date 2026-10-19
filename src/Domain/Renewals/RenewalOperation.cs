using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Contracts;
using Boilerate.Domain.Common.Exceptions;
namespace Boilerate.Domain.Renewals;

public sealed class RenewalOperation : BaseEntity
{
    public Guid RequestId { get; private set; }
    public Guid OperationId { get; private set; }
    public Guid ActorId { get; private set; }
    public string Action { get; private set; } = null!;
    public string RequestHash { get; private set; } = null!;
    public RenewalStatus StatusAfter { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }

    private RenewalOperation()
    {
    }

    public RenewalOperation(Guid requestId, Guid operationId, Guid actor, string action, string requestHash,
        RenewalStatus statusAfter, DateTime now)
    {
        RenewalRules.Id(requestId, "Request");
        RenewalRules.Id(operationId, "Operation");
        RenewalRules.Id(actor, "Actor");
        var validAction = CatalogRules.Required(action, 32, "Renewal action");
        if (validAction.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_'))
            throw new DomainException("Renewal actions must be ASCII identifiers.");
        RenewalRules.Hash(requestHash);
        if (!Enum.IsDefined(statusAfter))
            throw new DomainException("A valid renewal status is required.");
        var validNow = RenewalRules.Utc(now);
        RequestId = requestId;
        OperationId = operationId;
        ActorId = actor;
        Action = validAction;
        RequestHash = requestHash;
        StatusAfter = statusAfter;
        OccurredAtUtc = validNow;
    }
}
