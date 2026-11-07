using Boilerate.Domain.Renewals;

namespace Boilerate.Application.Renewals;

public sealed record SetRenewalTarget(string Version, Guid OperationId, string AccountIdentifier);

public sealed record ClaimRenewal(string Version, Guid OperationId);

public sealed record ReleaseRenewal(string Version, Guid OperationId, Guid ClaimToken);

public sealed record StartRenewal(string Version, Guid OperationId, Guid ClaimToken);

public sealed record VerifyRenewal(
    string Version,
    Guid OperationId,
    Guid AttemptId,
    bool Applied,
    DateTime? NewExpiresAtUtc,
    string Note,
    string? Evidence);

public sealed record ProvisionRenewals(string OrderVersion);

public sealed record RenewalDto(
    Guid Id,
    Guid OrderId,
    Guid OrderItemId,
    string Version,
    RenewalStatus Status,
    string Name,
    string Sku,
    string OptionDescription,
    int Quantity,
    string? TargetPreview,
    DateTime? CompletedAtUtc,
    DateTime? VerifiedExpiresAtUtc);

public sealed record RenewalAttemptDto(
    Guid Id,
    Guid OperationId,
    Guid StartedBy,
    DateTime StartedAtUtc,
    RenewalOutcome Outcome,
    Guid? VerifiedBy,
    DateTime? VerifiedAtUtc,
    DateTime? NewExpiresAtUtc);

public sealed record RenewalAdminDto(
    RenewalDto Renewal,
    Guid? ClaimedBy,
    DateTime? ClaimUntilUtc,
    Guid? ClaimToken,
    List<RenewalAttemptDto> Attempts);

public sealed record RenewalHistoryDto(
    Guid OperationId,
    string Action,
    Guid ActorId,
    RenewalStatus StatusAfter,
    DateTime OccurredAtUtc);

public sealed record RenewalContentDto(Guid Id, string Content);

