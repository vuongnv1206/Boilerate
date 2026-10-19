using Boilerate.Application.Catalog;
using Boilerate.Domain.Renewals;

namespace Boilerate.Application.Ordering;

public sealed record SetRenewalTarget(string Version, Guid OperationId, string AccountIdentifier);
public sealed record ClaimRenewal(string Version, Guid OperationId);
public sealed record ReleaseRenewal(string Version, Guid OperationId, Guid ClaimToken);
public sealed record StartRenewal(string Version, Guid OperationId, Guid ClaimToken);
public sealed record VerifyRenewal(string Version, Guid OperationId, Guid AttemptId, bool Applied,
    DateTime? NewExpiresAtUtc, string Note, string? Evidence);
public sealed record ProvisionRenewals(string OrderVersion);
public sealed record RenewalDto(Guid Id, Guid OrderId, Guid OrderItemId, string Version, RenewalStatus Status,
    string Name, string Sku, string OptionDescription, int Quantity, string? TargetPreview,
    DateTime? CompletedAtUtc, DateTime? VerifiedExpiresAtUtc);
public sealed record RenewalAttemptDto(Guid Id, Guid OperationId, Guid StartedBy, DateTime StartedAtUtc,
    RenewalOutcome Outcome, Guid? VerifiedBy, DateTime? VerifiedAtUtc, DateTime? NewExpiresAtUtc);
public sealed record RenewalAdminDto(RenewalDto Renewal, Guid? ClaimedBy, DateTime? ClaimUntilUtc,
    Guid? ClaimToken, List<RenewalAttemptDto> Attempts);
public sealed record RenewalHistoryDto(Guid OperationId, string Action, Guid ActorId, RenewalStatus StatusAfter, DateTime OccurredAtUtc);
public sealed record RenewalContentDto(Guid Id, string Content);

public interface IRenewalService
{
    Task<List<RenewalDto>> RenewalsAsync(Guid orderId, CancellationToken ct);
    Task<RenewalDto> SetRenewalTargetAsync(Guid orderId, Guid id, SetRenewalTarget request, CancellationToken ct);
    Task<RenewalContentDto> RevealRenewalTargetAsync(Guid orderId, Guid id, CancellationToken ct);
    Task<Page<RenewalAdminDto>> RenewalsAdminAsync(int page, int size, RenewalStatus? status, CancellationToken ct);
    Task<RenewalAdminDto> GetRenewalAdminAsync(Guid id, CancellationToken ct);
    Task<List<RenewalDto>> ProvisionRenewalsAsync(Guid orderId, ProvisionRenewals request, CancellationToken ct);
    Task<RenewalAdminDto> ClaimRenewalAsync(Guid id, ClaimRenewal request, CancellationToken ct);
    Task<RenewalAdminDto> ReleaseRenewalAsync(Guid id, ReleaseRenewal request, CancellationToken ct);
    Task<RenewalAdminDto> StartRenewalAsync(Guid id, StartRenewal request, CancellationToken ct);
    Task<RenewalAdminDto> VerifyRenewalAsync(Guid id, VerifyRenewal request, CancellationToken ct);
    Task<RenewalContentDto> RevealRenewalTargetAdminAsync(Guid id, CancellationToken ct);
    Task<RenewalContentDto> RevealRenewalEvidenceAsync(Guid id, Guid attemptId, CancellationToken ct);
    Task<List<RenewalHistoryDto>> RenewalHistoryAsync(Guid id, CancellationToken ct);
}
