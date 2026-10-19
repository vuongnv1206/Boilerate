using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using Boilerate.Domain.Renewals;
using Xunit;
namespace Boilerate.Tests;

public sealed class RenewalDomainTests
{
    private static readonly DateTime Now = new(2026, 10, 18, 11, 0, 0, DateTimeKind.Utc);
    private static readonly string TargetKey = new('a', 64);

    [Fact]
    public void RequestStartsWithNoTargetClaimOrPresumedExpiry()
    {
        var orderId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var request = new RenewalRequest(orderId, itemId, variantId);
        Assert.NotEqual(Guid.Empty, request.Id);
        Assert.Equal(orderId, request.OrderId);
        Assert.Equal(itemId, request.OrderItemId);
        Assert.Equal(variantId, request.VariantId);
        Assert.Equal(ProductFulfillmentKind.ManualExtension, request.FulfillmentKind);
        Assert.Equal(RenewalStatus.AwaitingTarget, request.Status);
        Assert.False(request.TargetLocked);
        Assert.Null(request.ProtectedTarget);
        Assert.Null(request.TargetKey);
        Assert.Null(request.TargetPreview);
        Assert.Null(request.ClaimedBy);
        Assert.Null(request.ClaimToken);
        Assert.Null(request.ClaimUntilUtc);
        Assert.Null(request.CompletedAtUtc);
        Assert.Null(request.VerifiedExpiresAtUtc);
        Assert.Empty(request.Attempts);
    }

    [Fact]
    public void RequestRequiresEverySnapshotLink()
    {
        var id = Guid.NewGuid();
        Assert.Throws<DomainException>(() => new RenewalRequest(Guid.Empty, id, id));
        Assert.Throws<DomainException>(() => new RenewalRequest(id, Guid.Empty, id));
        Assert.Throws<DomainException>(() => new RenewalRequest(id, id, Guid.Empty));
    }

    [Fact]
    public void AccountIdentifierIsTrimmedWithoutChangingItsActualCaseOrUnicode()
    {
        Assert.Equal("Account.Name@example.com", RenewalRequest.NormalizeTarget("  Account.Name@example.com  "));
        Assert.Equal("ＴàiKhoản", RenewalRequest.NormalizeTarget(" ＴàiKhoản "));
        Assert.Equal(new string('x', 256), RenewalRequest.NormalizeTarget(new string('x', 256)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("account\n")]
    [InlineData("\taccount")]
    [InlineData("acc\0ount")]
    [InlineData("acc\u007fount")]
    public void InvalidAccountIdentifiersAreRejected(string? value)
    {
        Assert.Throws<DomainException>(() => RenewalRequest.NormalizeTarget(value));
    }

    [Fact]
    public void AccountIdentifierLengthIsBounded()
    {
        Assert.Throws<DomainException>(() => RenewalRequest.NormalizeTarget(new string('x', 257)));
    }

    [Fact]
    public void TargetCanBeCorrectedBeforeAClaimAndTouchesVersion()
    {
        var request = NewRequest();
        var version = request.Version;
        request.SetTarget("ciphertext", TargetKey, "a***m");
        Assert.Equal(RenewalStatus.Ready, request.Status);
        Assert.Equal("ciphertext", request.ProtectedTarget);
        Assert.Equal(TargetKey, request.TargetKey);
        Assert.Equal("a***m", request.TargetPreview);
        Assert.NotEqual(version, request.Version);
        version = request.Version;
        request.SetTarget("new-ciphertext", new string('b', 64), "b***m");
        Assert.Equal("new-ciphertext", request.ProtectedTarget);
        Assert.Equal(new string('b', 64), request.TargetKey);
        Assert.NotEqual(version, request.Version);
    }

    [Fact]
    public void InvalidTargetDoesNotPartiallyChangeTheRequest()
    {
        var request = Ready();
        var version = request.Version;
        Assert.Throws<DomainException>(() => request.SetTarget(" ", TargetKey, "mask"));
        Assert.Throws<DomainException>(() => request.SetTarget(new string('x', 4001), TargetKey, "mask"));
        Assert.Throws<DomainException>(() => request.SetTarget("ciphertext", null!, "mask"));
        Assert.Throws<DomainException>(() => request.SetTarget("ciphertext", "bad", "mask"));
        Assert.Throws<DomainException>(() => request.SetTarget("ciphertext", new string('g', 64), "mask"));
        Assert.Throws<DomainException>(() => request.SetTarget("ciphertext", TargetKey, " "));
        Assert.Throws<DomainException>(() => request.SetTarget("ciphertext", TargetKey, new string('x', 101)));
        Assert.Equal("ciphertext", request.ProtectedTarget);
        Assert.Equal(TargetKey, request.TargetKey);
        Assert.Equal("a***m", request.TargetPreview);
        Assert.Equal(version, request.Version);
        Assert.Equal(RenewalStatus.Ready, request.Status);
    }

    [Fact]
    public void ClaimRequiresTargetAndValidActorDurationAndUtcTime()
    {
        Assert.Throws<DomainException>(() => NewRequest().Claim(Guid.NewGuid(), Now, 15));
        var request = Ready();
        var version = request.Version;
        Assert.Throws<DomainException>(() => request.Claim(Guid.Empty, Now, 15));
        Assert.Throws<DomainException>(() => request.Claim(Guid.NewGuid(), Now, 0));
        Assert.Throws<DomainException>(() => request.Claim(Guid.NewGuid(), Now, 121));
        Assert.Throws<DomainException>(() => request.Claim(Guid.NewGuid(), Unspecified(Now), 15));
        Assert.Throws<DomainException>(() => request.Claim(Guid.NewGuid(), DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc), 15));
        Assert.Equal(version, request.Version);
        Assert.Equal(RenewalStatus.Ready, request.Status);
        Assert.Null(request.ClaimToken);
    }

    [Fact]
    public void ActiveClaimBlocksOtherActorsTargetChangesAndWrongTokens()
    {
        var request = Ready();
        var actor = Guid.NewGuid();
        var token = request.Claim(actor, Now, 15);
        var version = request.Version;
        Assert.NotEqual(Guid.Empty, token);
        Assert.Equal(actor, request.ClaimedBy);
        Assert.Equal(token, request.ClaimToken);
        Assert.Equal(Now.AddMinutes(15), request.ClaimUntilUtc);
        Assert.Equal(RenewalStatus.Claimed, request.Status);
        Assert.Throws<DomainException>(() => request.Claim(Guid.NewGuid(), Now.AddMinutes(1), 15));
        Assert.Throws<DomainException>(() => request.Claim(actor, Now.AddMinutes(1), 15));
        Assert.Throws<DomainException>(() => request.SetTarget("replacement", TargetKey, "mask"));
        Assert.Throws<DomainException>(() => request.Start(Guid.NewGuid(), token, Guid.NewGuid(), Now));
        Assert.Throws<DomainException>(() => request.Start(actor, Guid.NewGuid(), Guid.NewGuid(), Now));
        Assert.Throws<DomainException>(() => request.Release(Guid.NewGuid(), token, Now));
        Assert.Throws<DomainException>(() => request.Release(actor, Guid.NewGuid(), Now));
        Assert.Equal(version, request.Version);
        Assert.Empty(request.Attempts);
    }

    [Fact]
    public void HolderCanReleaseAnUnstartedClaimAndTargetCanThenBeCorrected()
    {
        var request = Ready();
        var actor = Guid.NewGuid();
        var token = request.Claim(actor, Now, 15);
        var version = request.Version;
        request.Release(actor, token, Now.AddMinutes(1));
        Assert.Equal(RenewalStatus.Ready, request.Status);
        Assert.Null(request.ClaimedBy);
        Assert.Null(request.ClaimToken);
        Assert.Null(request.ClaimUntilUtc);
        Assert.False(request.TargetLocked);
        Assert.NotEqual(version, request.Version);
        request.SetTarget("replacement", TargetKey, "mask");
        Assert.Equal("replacement", request.ProtectedTarget);
    }

    [Fact]
    public void ClaimCanBeReclaimedAtItsExactExpiryButTheOldTokenCannotStart()
    {
        var request = Ready();
        var firstActor = Guid.NewGuid();
        var firstToken = request.Claim(firstActor, Now, 15);
        Assert.Throws<DomainException>(() => request.Start(firstActor, firstToken, Guid.NewGuid(), Now.AddMinutes(15)));
        Assert.Throws<DomainException>(() => request.Release(firstActor, firstToken, Now.AddMinutes(15)));
        var secondActor = Guid.NewGuid();
        var secondToken = request.Claim(secondActor, Now.AddMinutes(15), 1);
        Assert.NotEqual(firstToken, secondToken);
        Assert.Equal(secondActor, request.ClaimedBy);
        Assert.Equal(Now.AddMinutes(16), request.ClaimUntilUtc);
        Assert.Throws<DomainException>(() => request.Start(firstActor, firstToken, Guid.NewGuid(), Now.AddMinutes(15)));
        Assert.Equal(RenewalStatus.Claimed, request.Status);
        Assert.Empty(request.Attempts);
    }

    [Fact]
    public void ExpirationOnlyReleasesUnstartedClaimsAndDoesNotTouchOtherStates()
    {
        var request = Ready();
        var readyVersion = request.Version;
        Assert.False(request.ReleaseExpired(Now));
        Assert.Equal(readyVersion, request.Version);
        request.Claim(Guid.NewGuid(), Now, 15);
        var version = request.Version;
        Assert.False(request.ReleaseExpired(Now.AddMinutes(15).AddTicks(-10)));
        Assert.Equal(version, request.Version);
        Assert.True(request.ReleaseExpired(Now.AddMinutes(15)));
        Assert.Equal(RenewalStatus.Ready, request.Status);
        Assert.Null(request.ClaimedBy);
        Assert.Null(request.ClaimToken);
        Assert.Null(request.ClaimUntilUtc);
        Assert.NotEqual(version, request.Version);
        version = request.Version;
        Assert.False(request.ReleaseExpired(Now.AddYears(1)));
        Assert.Equal(version, request.Version);
    }

    [Fact]
    public void StartDurablyRequiresVerificationAndRemovesItsExpiringLease()
    {
        var request = Ready();
        var actor = Guid.NewGuid();
        var token = request.Claim(actor, Now, 15);
        var operation = Guid.NewGuid();
        var version = request.Version;
        var attempt = request.Start(actor, token, operation, Now.AddMinutes(1));
        Assert.Equal(RenewalStatus.PendingVerification, request.Status);
        Assert.True(request.TargetLocked);
        Assert.Equal(actor, request.ClaimedBy);
        Assert.Null(request.ClaimToken);
        Assert.Null(request.ClaimUntilUtc);
        Assert.NotEqual(version, request.Version);
        Assert.Same(attempt, Assert.Single(request.Attempts));
        Assert.Equal(request.Id, attempt.RequestId);
        Assert.Equal(operation, attempt.OperationId);
        Assert.Equal(actor, attempt.StartedBy);
        Assert.Equal(Now.AddMinutes(1), attempt.StartedAtUtc);
        Assert.Equal(RenewalOutcome.Pending, attempt.Outcome);
        Assert.Null(attempt.VerifiedBy);
        Assert.Null(attempt.VerifiedAtUtc);
        Assert.Null(attempt.NewExpiresAtUtc);
        Assert.Null(attempt.ProtectedEvidence);
    }

    [Fact]
    public void UnknownOutcomeNeverReopensAfterTimeoutOrAnOldToken()
    {
        var request = Started(out var actor, out _);
        var version = request.Version;
        Assert.False(request.ReleaseExpired(Now.AddYears(1)));
        Assert.Throws<DomainException>(() => request.Claim(Guid.NewGuid(), Now.AddYears(1), 15));
        Assert.Throws<DomainException>(() => request.Release(actor, Guid.NewGuid(), Now.AddYears(1)));
        Assert.Throws<DomainException>(() => request.Start(actor, Guid.NewGuid(), Guid.NewGuid(), Now.AddYears(1)));
        Assert.Throws<DomainException>(() => request.SetTarget("replacement", TargetKey, "mask"));
        Assert.Equal(RenewalStatus.PendingVerification, request.Status);
        Assert.Equal(version, request.Version);
        Assert.Single(request.Attempts);
    }

    [Fact]
    public void EmptyStartOperationDoesNotConsumeAValidClaim()
    {
        var request = Ready();
        var actor = Guid.NewGuid();
        var token = request.Claim(actor, Now, 15);
        var version = request.Version;
        Assert.Throws<DomainException>(() => request.Start(actor, token, Guid.Empty, Now));
        Assert.Equal(version, request.Version);
        Assert.Equal(token, request.ClaimToken);
        Assert.False(request.TargetLocked);
        Assert.Equal(RenewalStatus.Claimed, request.Status);
        Assert.Empty(request.Attempts);
    }

    [Fact]
    public void VerificationStoresActualExpiryAndVerifierRatherThanComputingADuration()
    {
        var request = Started(out var actor, out var attempt);
        var verifier = Guid.NewGuid();
        var expiry = Now.AddDays(47).AddHours(3);
        var version = request.Version;
        request.Verify(attempt.Id, true, expiry, verifier, Now.AddMinutes(2), "protected-note-and-evidence");
        Assert.NotEqual(actor, verifier);
        Assert.Equal(RenewalStatus.Completed, request.Status);
        Assert.True(request.TargetLocked);
        Assert.Equal(expiry, request.VerifiedExpiresAtUtc);
        Assert.Equal(Now.AddMinutes(2), request.CompletedAtUtc);
        Assert.Null(request.ClaimedBy);
        Assert.Null(request.ClaimToken);
        Assert.Null(request.ClaimUntilUtc);
        Assert.NotEqual(version, request.Version);
        Assert.Equal(RenewalOutcome.Applied, attempt.Outcome);
        Assert.Equal(verifier, attempt.VerifiedBy);
        Assert.Equal(Now.AddMinutes(2), attempt.VerifiedAtUtc);
        Assert.Equal(expiry, attempt.NewExpiresAtUtc);
        Assert.Equal("protected-note-and-evidence", attempt.ProtectedEvidence);
    }

    [Fact]
    public void NotAppliedRequiresAnotherExplicitClaimAndStartAndPreservesEarlierEvidence()
    {
        var request = Started(out var actor, out var first);
        request.Verify(first.Id, false, null, Guid.NewGuid(), Now.AddMinutes(2), "confirmed-not-applied");
        Assert.Equal(RenewalStatus.Ready, request.Status);
        Assert.True(request.TargetLocked);
        Assert.Null(request.CompletedAtUtc);
        Assert.Null(request.VerifiedExpiresAtUtc);
        Assert.Null(first.NewExpiresAtUtc);
        Assert.Equal(RenewalOutcome.NotApplied, first.Outcome);
        Assert.Throws<DomainException>(() => request.SetTarget("another-account", new string('b', 64), "mask"));
        Assert.Throws<DomainException>(() => request.Start(actor, Guid.NewGuid(), Guid.NewGuid(), Now.AddMinutes(3)));
        var newActor = Guid.NewGuid();
        var token = request.Claim(newActor, Now.AddMinutes(3), 15);
        var second = request.Start(newActor, token, Guid.NewGuid(), Now.AddMinutes(4));
        var pendingVersion = request.Version;
        Assert.Throws<DomainException>(() => request.Verify(first.Id, true, Now.AddDays(90), newActor,
            Now.AddMinutes(5), "cannot-rewrite-the-earlier-result"));
        Assert.Equal(pendingVersion, request.Version);
        Assert.Equal(RenewalOutcome.Pending, second.Outcome);
        request.Verify(second.Id, true, Now.AddDays(60), Guid.NewGuid(), Now.AddMinutes(5), "confirmed-applied");
        Assert.Equal(2, request.Attempts.Count);
        Assert.Equal(RenewalOutcome.NotApplied, first.Outcome);
        Assert.Equal("confirmed-not-applied", first.ProtectedEvidence);
        Assert.Equal(RenewalOutcome.Applied, second.Outcome);
        Assert.Equal(RenewalStatus.Completed, request.Status);
        Assert.Equal(Now.AddDays(60), request.VerifiedExpiresAtUtc);
    }

    [Fact]
    public void EarlierStartOperationCannotBeReusedForAnotherExternalAttempt()
    {
        var request = Started(out var actor, out var first);
        request.Verify(first.Id, false, null, actor, Now.AddMinutes(2), "not-applied");
        var token = request.Claim(actor, Now.AddMinutes(3), 15);
        var version = request.Version;
        Assert.Throws<DomainException>(() => request.Start(actor, token, first.OperationId, Now.AddMinutes(4)));
        Assert.Equal(version, request.Version);
        Assert.Equal(RenewalStatus.Claimed, request.Status);
        Assert.Equal(token, request.ClaimToken);
        Assert.Single(request.Attempts);
    }

    [Fact]
    public void CompletedRequestCannotBeRepeatedOrHaveItsVerifiedResultChanged()
    {
        var request = Started(out _, out var attempt);
        var verifier = Guid.NewGuid();
        request.Verify(attempt.Id, true, Now.AddDays(30), verifier, Now.AddMinutes(2), "original-evidence");
        var version = request.Version;
        Assert.Throws<DomainException>(() => request.Verify(attempt.Id, false, null, verifier, Now.AddMinutes(3), "replacement"));
        Assert.Throws<DomainException>(() => request.Claim(Guid.NewGuid(), Now.AddYears(1), 15));
        Assert.Throws<DomainException>(() => request.SetTarget("replacement", TargetKey, "mask"));
        Assert.False(request.ReleaseExpired(Now.AddYears(1)));
        Assert.Equal(version, request.Version);
        Assert.Equal(RenewalOutcome.Applied, attempt.Outcome);
        Assert.Equal("original-evidence", attempt.ProtectedEvidence);
        Assert.Equal(Now.AddDays(30), attempt.NewExpiresAtUtc);
    }

    [Fact]
    public void VerificationRejectsWrongAttemptActorTimesExpiryAndMissingEvidenceAtomically()
    {
        var request = Started(out var actor, out var attempt);
        var version = request.Version;
        var verifyTime = Now.AddMinutes(2);
        var expiry = Now.AddDays(30);
        Assert.Throws<DomainException>(() => request.Verify(Guid.Empty, true, expiry, actor, verifyTime, "evidence"));
        Assert.Throws<DomainException>(() => request.Verify(Guid.NewGuid(), true, expiry, actor, verifyTime, "evidence"));
        Assert.Throws<DomainException>(() => request.Verify(attempt.Id, true, expiry, Guid.Empty, verifyTime, "evidence"));
        Assert.Throws<DomainException>(() => request.Verify(attempt.Id, true, expiry, actor, Unspecified(verifyTime), "evidence"));
        Assert.Throws<DomainException>(() => request.Verify(attempt.Id, true, expiry, actor, Now, "evidence"));
        Assert.Throws<DomainException>(() => request.Verify(attempt.Id, true, null, actor, verifyTime, "evidence"));
        Assert.Throws<DomainException>(() => request.Verify(attempt.Id, true, verifyTime, actor, verifyTime, "evidence"));
        Assert.Throws<DomainException>(() => request.Verify(attempt.Id, true, verifyTime.AddTicks(-10), actor, verifyTime, "evidence"));
        Assert.Throws<DomainException>(() => request.Verify(attempt.Id, true, Unspecified(expiry), actor, verifyTime, "evidence"));
        Assert.Throws<DomainException>(() => request.Verify(attempt.Id, false, expiry, actor, verifyTime, "evidence"));
        Assert.Throws<DomainException>(() => request.Verify(attempt.Id, true, expiry, actor, verifyTime, " "));
        Assert.Throws<DomainException>(() => request.Verify(attempt.Id, true, expiry, actor, verifyTime, new string('x', 32001)));
        Assert.Equal(version, request.Version);
        Assert.Equal(RenewalStatus.PendingVerification, request.Status);
        Assert.Equal(RenewalOutcome.Pending, attempt.Outcome);
        Assert.Null(attempt.VerifiedAtUtc);
        Assert.Null(attempt.ProtectedEvidence);
        Assert.Null(request.CompletedAtUtc);
        request.Verify(attempt.Id, true, expiry, actor, verifyTime, new string('x', 32000));
        Assert.Equal(RenewalStatus.Completed, request.Status);
    }

    [Fact]
    public void NonPendingRequestCannotVerifyAnyAttempt()
    {
        var request = Ready();
        var version = request.Version;
        Assert.Throws<DomainException>(() => request.Verify(Guid.NewGuid(), true, Now.AddDays(1), Guid.NewGuid(), Now, "evidence"));
        Assert.Equal(version, request.Version);
        Assert.Equal(RenewalStatus.Ready, request.Status);
    }

    [Fact]
    public void AllRenewalTimesAreNormalizedToDatabaseMicroseconds()
    {
        var request = Ready();
        var actor = Guid.NewGuid();
        var token = request.Claim(actor, Now.AddTicks(1234567), 15);
        Assert.Equal(Now.AddMinutes(15).AddTicks(1234560), request.ClaimUntilUtc);
        var attempt = request.Start(actor, token, Guid.NewGuid(), Now.AddMinutes(1).AddTicks(2345678));
        Assert.Equal(Now.AddMinutes(1).AddTicks(2345670), attempt.StartedAtUtc);
        request.Verify(attempt.Id, true, Now.AddDays(30).AddTicks(4567891), actor,
            Now.AddMinutes(2).AddTicks(3456789), "evidence");
        Assert.Equal(Now.AddMinutes(2).AddTicks(3456780), request.CompletedAtUtc);
        Assert.Equal(Now.AddDays(30).AddTicks(4567890), request.VerifiedExpiresAtUtc);
        Assert.Equal(request.CompletedAtUtc, attempt.VerifiedAtUtc);
        Assert.Equal(request.VerifiedExpiresAtUtc, attempt.NewExpiresAtUtc);
        Assert.Equal(DateTimeKind.Utc, attempt.StartedAtUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, attempt.VerifiedAtUtc!.Value.Kind);
        Assert.Equal(DateTimeKind.Utc, request.VerifiedExpiresAtUtc!.Value.Kind);
    }

    [Fact]
    public void EveryClaimMutationRejectsNonUtcTimesAndMissingHolderIdentifiers()
    {
        var request = Ready();
        var actor = Guid.NewGuid();
        var token = request.Claim(actor, Now, 15);
        var version = request.Version;
        Assert.Throws<DomainException>(() => request.Release(Guid.Empty, token, Now));
        Assert.Throws<DomainException>(() => request.Release(actor, Guid.Empty, Now));
        Assert.Throws<DomainException>(() => request.Release(actor, token, Unspecified(Now)));
        Assert.Throws<DomainException>(() => request.Start(Guid.Empty, token, Guid.NewGuid(), Now));
        Assert.Throws<DomainException>(() => request.Start(actor, Guid.Empty, Guid.NewGuid(), Now));
        Assert.Throws<DomainException>(() => request.Start(actor, token, Guid.NewGuid(), Unspecified(Now)));
        Assert.Throws<DomainException>(() => request.ReleaseExpired(Unspecified(Now)));
        Assert.Equal(version, request.Version);
        Assert.Equal(token, request.ClaimToken);
        Assert.Empty(request.Attempts);
    }

    [Fact]
    public void AttemptRequiresRequestOperationActorAndUtcTime()
    {
        var id = Guid.NewGuid();
        Assert.Throws<DomainException>(() => new RenewalAttempt(Guid.Empty, id, id, Now));
        Assert.Throws<DomainException>(() => new RenewalAttempt(id, Guid.Empty, id, Now));
        Assert.Throws<DomainException>(() => new RenewalAttempt(id, id, Guid.Empty, Now));
        Assert.Throws<DomainException>(() => new RenewalAttempt(id, id, id, Unspecified(Now)));
    }

    [Fact]
    public void OperationKeepsReplayMetadataAndNoTargetEvidenceOrClaimToken()
    {
        var requestId = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var operation = new RenewalOperation(requestId, operationId, actor, "VerifyApplied", TargetKey,
            RenewalStatus.Completed, Now.AddTicks(1234567));
        Assert.NotEqual(Guid.Empty, operation.Id);
        Assert.Equal(requestId, operation.RequestId);
        Assert.Equal(operationId, operation.OperationId);
        Assert.Equal(actor, operation.ActorId);
        Assert.Equal("VerifyApplied", operation.Action);
        Assert.Equal(TargetKey, operation.RequestHash);
        Assert.Equal(RenewalStatus.Completed, operation.StatusAfter);
        Assert.Equal(Now.AddTicks(1234560), operation.OccurredAtUtc);
        Assert.Equal(DateTimeKind.Utc, operation.OccurredAtUtc.Kind);
    }

    [Fact]
    public void OperationRejectsMissingLinksMalformedHashActionStatusAndNonUtcTime()
    {
        var id = Guid.NewGuid();
        Assert.Throws<DomainException>(() => new RenewalOperation(Guid.Empty, id, id, "Claim", TargetKey, RenewalStatus.Claimed, Now));
        Assert.Throws<DomainException>(() => new RenewalOperation(id, Guid.Empty, id, "Claim", TargetKey, RenewalStatus.Claimed, Now));
        Assert.Throws<DomainException>(() => new RenewalOperation(id, id, Guid.Empty, "Claim", TargetKey, RenewalStatus.Claimed, Now));
        Assert.Throws<DomainException>(() => new RenewalOperation(id, id, id, " ", TargetKey, RenewalStatus.Claimed, Now));
        Assert.Throws<DomainException>(() => new RenewalOperation(id, id, id, new string('x', 33), TargetKey, RenewalStatus.Claimed, Now));
        Assert.Throws<DomainException>(() => new RenewalOperation(id, id, id, "Claim me", TargetKey, RenewalStatus.Claimed, Now));
        Assert.Throws<DomainException>(() => new RenewalOperation(id, id, id, "Cláim", TargetKey, RenewalStatus.Claimed, Now));
        Assert.Throws<DomainException>(() => new RenewalOperation(id, id, id, "Claim", null!, RenewalStatus.Claimed, Now));
        Assert.Throws<DomainException>(() => new RenewalOperation(id, id, id, "Claim", "bad", RenewalStatus.Claimed, Now));
        Assert.Throws<DomainException>(() => new RenewalOperation(id, id, id, "Claim", new string('g', 64), RenewalStatus.Claimed, Now));
        Assert.Throws<DomainException>(() => new RenewalOperation(id, id, id, "Claim", TargetKey, (RenewalStatus)0, Now));
        Assert.Throws<DomainException>(() => new RenewalOperation(id, id, id, "Claim", TargetKey, RenewalStatus.Claimed, Unspecified(Now)));
    }

    private static RenewalRequest NewRequest() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    private static RenewalRequest Ready()
    {
        var request = NewRequest();
        request.SetTarget("ciphertext", TargetKey, "a***m");
        return request;
    }

    private static RenewalRequest Started(out Guid actor, out RenewalAttempt attempt)
    {
        var request = Ready();
        actor = Guid.NewGuid();
        var token = request.Claim(actor, Now, 15);
        attempt = request.Start(actor, token, Guid.NewGuid(), Now.AddMinutes(1));
        return request;
    }

    private static DateTime Unspecified(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
}
