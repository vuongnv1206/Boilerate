using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Boilerate.Application.Catalog;
using Boilerate.Application.Common.Events;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Identity.Users;
using Boilerate.Application.Identity.Tokens;
using Boilerate.Application.Ordering;
using Boilerate.Domain.Auditing;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using Boilerate.Domain.Ordering;
using Boilerate.Domain.Renewals;
using Boilerate.Infrastructure.Commerce;
using Boilerate.Infrastructure.Persistence.Context;
using Boilerate.Infrastructure.Persistence.Initialization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Boilerate.Tests;

[Collection("Foundation")]
public sealed class RenewalIntegrationTests
{
    private static CommerceService Catalog(IServiceScope scope)
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = Substitute.For<IUserService>();
        users.HasPermissionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        return new(db, new CommerceUnitOfWork(db), scope.ServiceProvider.GetRequiredService<ICurrentUser>(), users,
            Options.Create(new CommerceSettings()), scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>());
    }

    private static OrderingService Ordering(IServiceScope scope, TimeProvider? clock = null, string? deniedPermission = null,
        ApplicationDbContext? dbOverride = null)
    {
        var db = dbOverride ?? scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = Substitute.For<IUserService>();
        users.HasPermissionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => (string)call[1] != deniedPermission);
        return new(db, new CommerceUnitOfWork(db), scope.ServiceProvider.GetRequiredService<ICurrentUser>(), users,
            Options.Create(new OrderingSettings()), clock ?? TimeProvider.System,
            scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>());
    }

    private static async Task<ProductDto> Product(IServiceScope scope, string slug,
        ProductFulfillmentKind kind = ProductFulfillmentKind.ManualExtension)
    {
        var catalog = Catalog(scope);
        await catalog.InitializeAsync(default);
        var type = await catalog.CreateTypeAsync(new(slug.ToUpperInvariant(), "Renewal packages", []), default);
        var product = await catalog.CreateAsync(new(type.Id, type.Version,
            new("30 days " + slug, slug, null, [], [], []), [], [new(slug, 100, null, new())], kind), default);
        return await catalog.StatusAsync(new(product.Id, product.Version, ProductStatus.Published), default);
    }

    private static async Task<OrderDto> Checkout(OrderingService ordering, params (Guid VariantId, int Quantity)[] lines)
    {
        var current = await ordering.GetCartAsync(default);
        var cart = await ordering.SaveCartAsync(new(current.Version,
            lines.Select(i => new CartLineInput(i.VariantId, i.Quantity)).ToList()), default);
        return await ordering.CheckoutAsync(new(cart.Version!, Guid.NewGuid(), OrderPaymentMethod.Prepaid,
            "Renewal customer", "customer@example.test", "0901234567", "123 Test Street", cart.Total), default);
    }

    private static ConfirmPayment Receipt(OrderDto order, TimeProvider? clock = null) => new(order.Version,
        Guid.NewGuid(), "RENEWAL-" + Guid.NewGuid().ToString("N"), order.Total, "VND",
        (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime.AddSeconds(-1));

    private static async Task<RenewalDto> PaidRenewal(IServiceScope scope, ProductDto product, int quantity = 1,
        TimeProvider? clock = null)
    {
        var ordering = Ordering(scope, clock);
        var order = await Checkout(ordering, (product.Variants.Single().Id, quantity));
        await ordering.ConfirmPaymentAsync(order.Id, Receipt(order, clock), default);
        return Assert.Single(await ordering.RenewalsAsync(order.Id, default));
    }

    private static async Task<RenewalAdminDto> Start(OrderingService ordering, RenewalDto renewal)
    {
        var claim = await ordering.ClaimRenewalAsync(renewal.Id, new(renewal.Version, Guid.NewGuid()), default);
        return await ordering.StartRenewalAsync(renewal.Id,
            new(claim.Renewal.Version, Guid.NewGuid(), claim.ClaimToken!.Value), default);
    }

    private static async Task RejectTransition(Func<Task> action)
    {
        var error = await Record.ExceptionAsync(action);
        Assert.True(error is DomainException or ConflictException, error?.ToString() ?? "The state transition was unexpectedly accepted.");
    }

    private sealed class MutableClock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan delta) => _now += delta;
    }

    private sealed class CommitFailure : DbTransactionInterceptor
    {
        public InvalidOperationException Error { get; } = new("Injected renewal failure after writes and before commit.");
        public bool WasReached { get; private set; }
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
            TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            WasReached = true;
            throw Error;
        }
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task PaidConfirmationCreatesOneRenewalPerLineWithoutInventingAnExpiryAndReplayDoesNotCreateMore(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var product = await Product(scope, "renewal-paid");
        var ordering = Ordering(scope);
        var order = await Checkout(ordering, (product.Variants.Single().Id, 2));
        Assert.Empty(await db.Set<RenewalRequest>().AsNoTracking().ToListAsync());
        var receipt = Receipt(order);
        var paid = await ordering.ConfirmPaymentAsync(order.Id, receipt, default);
        var renewal = Assert.Single(await ordering.RenewalsAsync(order.Id, default));
        Assert.Equal(order.Items.Single().Id, renewal.OrderItemId);
        Assert.Equal(2, renewal.Quantity);
        Assert.Equal(RenewalStatus.AwaitingTarget, renewal.Status);
        Assert.Null(renewal.VerifiedExpiresAtUtc);
        Assert.Null(renewal.CompletedAtUtc);
        Assert.Equal(OrderPaymentStatus.Paid, paid.PaymentStatus);
        Assert.Equal(OrderFulfillmentStatus.InProgress, paid.FulfillmentStatus);
        Assert.False((await db.Orders.AsNoTracking().SingleAsync()).PhysicalDispatchStarted);
        await ordering.ConfirmPaymentAsync(order.Id, receipt, default);
        Assert.Single(await db.Set<RenewalRequest>().AsNoTracking().ToListAsync());
        Assert.Empty(await db.Set<RenewalAttempt>().AsNoTracking().ToListAsync());
        Assert.Empty(await db.DigitalDeliveries.AsNoTracking().ToListAsync());
        Assert.Empty(await db.OutboxMessages.AsNoTracking().ToListAsync());
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task AdminOrderHistoryIncludesIndirectRenewalRequestAndAttemptAudit(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var product = await Product(scope, "renewal-order-history");
        var ordering = Ordering(scope);
        var renewal = await PaidRenewal(scope, product);
        renewal = await ordering.SetRenewalTargetAsync(renewal.OrderId, renewal.Id,
            new(renewal.Version, Guid.NewGuid(), "renewal-history-account"), default);
        await Start(ordering, renewal);

        var history = await ordering.GetOrderHistoryAsync(renewal.OrderId, default);

        Assert.Contains(history, entry => entry.RelatedEntityType == "RenewalRequests");
        Assert.Contains(history, entry => entry.RelatedEntityType == "RenewalAttempts");
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task OnlyOwnerSetsOrReadsTargetAndRegularResponsesAndAuditNeverExposeProtectedContent(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        var owner = Guid.NewGuid();
        using var scope = fixture.Scope(owner);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var product = await Product(scope, "renewal-private");
        var ordering = Ordering(scope);
        var renewal = await PaidRenewal(scope, product);
        const string target = "CaseSensitive.Customer@example.test";
        var targetOperation = new SetRenewalTarget(renewal.Version, Guid.NewGuid(), "  " + target + "  ");
        var ready = await ordering.SetRenewalTargetAsync(renewal.OrderId, renewal.Id, targetOperation, default);
        Assert.Equal(RenewalStatus.Ready, ready.Status);
        Assert.NotEqual(target, ready.TargetPreview);
        Assert.DoesNotContain(target, JsonSerializer.Serialize(ready));
        var stored = await db.Set<RenewalRequest>().AsNoTracking().SingleAsync();
        Assert.NotEqual(target, stored.ProtectedTarget);
        Assert.DoesNotContain(target, stored.ProtectedTarget!);
        var before = await db.Trails.CountAsync(t => t.Type == TrailType.Read);
        Assert.Equal(target, (await ordering.RevealRenewalTargetAsync(renewal.OrderId, renewal.Id, default)).Content);
        Assert.Equal(before + 1, await db.Trails.CountAsync(t => t.Type == TrailType.Read));
        using var otherScope = fixture.Scope(Guid.NewGuid());
        var other = Ordering(otherScope);
        await Assert.ThrowsAsync<NotFoundException>(() => other.RenewalsAsync(renewal.OrderId, default));
        await Assert.ThrowsAsync<NotFoundException>(() => other.RevealRenewalTargetAsync(renewal.OrderId, renewal.Id, default));
        await Assert.ThrowsAsync<NotFoundException>(() => other.SetRenewalTargetAsync(renewal.OrderId, renewal.Id,
            targetOperation with { Version = ready.Version, OperationId = Guid.NewGuid() }, default));
        await Assert.ThrowsAsync<ForbiddenException>(() => Ordering(otherScope, deniedPermission: "Permissions.Renewal.Reveal")
            .RevealRenewalTargetAdminAsync(renewal.Id, default));
        Assert.Equal(target, (await other.RevealRenewalTargetAdminAsync(renewal.Id, default)).Content);
        var admin = await other.GetRenewalAdminAsync(renewal.Id, default);
        var page = await other.RenewalsAdminAsync(1, 20, null, default);
        var metadata = JsonSerializer.Serialize(new { admin, page });
        Assert.DoesNotContain(target, metadata);
        Assert.DoesNotContain(stored.ProtectedTarget!, metadata);
        Assert.DoesNotContain(stored.TargetKey!, metadata);
        var trails = JsonSerializer.Serialize(await db.Trails.AsNoTracking().ToListAsync());
        Assert.DoesNotContain(target, trails);
        Assert.DoesNotContain(stored.ProtectedTarget!, trails);
        Assert.DoesNotContain(stored.TargetKey!, trails);
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task LeaseAllowsHandoffOnlyBeforeStartAndRestartCannotReclaimAnUnknownExternalOutcome(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        var clock = new MutableClock();
        var owner = Guid.NewGuid();
        using var ownerScope = fixture.Scope(owner);
        var product = await Product(ownerScope, "renewal-handoff");
        var renewal = await PaidRenewal(ownerScope, product, clock: clock);
        renewal = await Ordering(ownerScope, clock).SetRenewalTargetAsync(renewal.OrderId, renewal.Id,
            new(renewal.Version, Guid.NewGuid(), "handoff-account"), default);
        var staffA = Guid.NewGuid();
        using var scopeA = fixture.Scope(staffA);
        var first = await Ordering(scopeA, clock).ClaimRenewalAsync(renewal.Id, new(renewal.Version, Guid.NewGuid()), default);
        Assert.NotNull(first.ClaimToken);
        var staffB = Guid.NewGuid();
        using var scopeB = fixture.Scope(staffB);
        var orderingB = Ordering(scopeB, clock);
        var seen = await orderingB.GetRenewalAdminAsync(renewal.Id, default);
        Assert.Null(seen.ClaimToken);
        Assert.Null((await orderingB.RenewalsAdminAsync(1, 20, null, default)).Items.Single().ClaimToken);
        await RejectTransition(() => orderingB.ClaimRenewalAsync(renewal.Id, new(first.Renewal.Version, Guid.NewGuid()), default));
        clock.Advance(TimeSpan.FromMinutes(16));
        var takeover = await orderingB.ClaimRenewalAsync(renewal.Id, new(first.Renewal.Version, Guid.NewGuid()), default);
        await RejectTransition(() => Ordering(scopeA, clock).StartRenewalAsync(renewal.Id,
            new(takeover.Renewal.Version, Guid.NewGuid(), first.ClaimToken!.Value), default));
        var startOperation = new StartRenewal(takeover.Renewal.Version, Guid.NewGuid(), takeover.ClaimToken!.Value);
        var started = await orderingB.StartRenewalAsync(renewal.Id, startOperation, default);
        Assert.Equal(RenewalStatus.PendingVerification, started.Renewal.Status);
        Assert.Null(started.ClaimToken);
        Assert.Null(started.ClaimUntilUtc);
        Assert.Equal(staffB, Assert.Single(started.Attempts).StartedBy);
        clock.Advance(TimeSpan.FromDays(2));
        using var restartedScope = fixture.Scope(staffB);
        var restarted = Ordering(restartedScope, clock);
        var replay = await restarted.StartRenewalAsync(renewal.Id,
            startOperation with { Version = "stale-after-restart", ClaimToken = Guid.NewGuid() }, default);
        Assert.Equal(started.Attempts.Single().Id, Assert.Single(replay.Attempts).Id);
        await RejectTransition(() => restarted.ClaimRenewalAsync(renewal.Id, new(replay.Renewal.Version, Guid.NewGuid()), default));
        await RejectTransition(() => Ordering(ownerScope, clock).SetRenewalTargetAsync(renewal.OrderId, renewal.Id,
            new(replay.Renewal.Version, Guid.NewGuid(), "different-account"), default));
        var history = await restarted.RenewalHistoryAsync(renewal.Id, default);
        Assert.Single(history.Where(h => h.Action == "Start"));
        Assert.Contains(history, h => h.Action == "ClaimExpired");
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task SameCanonicalTargetAcrossOrdersIsSerializedAndExpiredUnstartedClaimIsReleasedAtomically(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        var clock = new MutableClock();
        using var scope = fixture.Scope(Guid.NewGuid());
        var product = await Product(scope, "renewal-shared-target");
        var ordering = Ordering(scope, clock);
        var first = await PaidRenewal(scope, product, clock: clock);
        var second = await PaidRenewal(scope, product, clock: clock);
        first = await ordering.SetRenewalTargetAsync(first.OrderId, first.Id,
            new(first.Version, Guid.NewGuid(), " Shared-Account "), default);
        second = await ordering.SetRenewalTargetAsync(second.OrderId, second.Id,
            new(second.Version, Guid.NewGuid(), "shared-account"), default);
        var claimed = await ordering.ClaimRenewalAsync(first.Id, new(first.Version, Guid.NewGuid()), default);
        await Assert.ThrowsAsync<ConflictException>(() => ordering.ClaimRenewalAsync(second.Id, new(second.Version, Guid.NewGuid()), default));
        clock.Advance(TimeSpan.FromMinutes(16));
        var next = await ordering.ClaimRenewalAsync(second.Id, new(second.Version, Guid.NewGuid()), default);
        Assert.Equal(RenewalStatus.Ready, (await ordering.GetRenewalAdminAsync(first.Id, default)).Renewal.Status);
        var pending = await ordering.StartRenewalAsync(second.Id,
            new(next.Renewal.Version, Guid.NewGuid(), next.ClaimToken!.Value), default);
        clock.Advance(TimeSpan.FromDays(1));
        var latestFirst = await ordering.GetRenewalAdminAsync(first.Id, default);
        await Assert.ThrowsAsync<ConflictException>(() => ordering.ClaimRenewalAsync(first.Id,
            new(latestFirst.Renewal.Version, Guid.NewGuid()), default));
        Assert.Equal(RenewalStatus.PendingVerification, pending.Renewal.Status);
        Assert.Contains(await ordering.RenewalHistoryAsync(first.Id, default), h => h.Action == "ClaimExpired");
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task ConcurrentStaffClaimsPermitOnlyOneHolderAndOnlyHolderReceivesTheToken(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var product = await Product(scope, "renewal-race");
        var renewal = await PaidRenewal(scope, product);
        renewal = await Ordering(scope).SetRenewalTargetAsync(renewal.OrderId, renewal.Id,
            new(renewal.Version, Guid.NewGuid(), "race-account"), default);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<RenewalAdminDto?> Claim()
        {
            using var worker = fixture.Scope(Guid.NewGuid());
            await signal.Task;
            try { return await Ordering(worker).ClaimRenewalAsync(renewal.Id, new(renewal.Version, Guid.NewGuid()), timeout.Token); }
            catch (ConflictException) { return null; }
        }
        var a = Claim();
        var b = Claim();
        signal.SetResult();
        var won = Assert.Single((await Task.WhenAll(a, b)).Where(r => r is not null))!;
        Assert.NotNull(won.ClaimToken);
        using var observer = fixture.Scope(Guid.NewGuid());
        Assert.Null((await Ordering(observer).GetRenewalAdminAsync(renewal.Id, default)).ClaimToken);
        Assert.Single((await Ordering(observer).RenewalHistoryAsync(renewal.Id, default)).Where(h => h.Action == "Claim"));
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task OperationReplayChecksActorRequestAndContentBeforeVersionAndDoesNotBypassPermission(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var product = await Product(scope, "renewal-replay");
        var ordering = Ordering(scope);
        var renewal = await PaidRenewal(scope, product);
        var set = new SetRenewalTarget(renewal.Version, Guid.NewGuid(), "replay-account");
        var ready = await ordering.SetRenewalTargetAsync(renewal.OrderId, renewal.Id, set, default);
        Assert.Equal(ready.Version, (await ordering.SetRenewalTargetAsync(renewal.OrderId, renewal.Id,
            set with { Version = "stale", AccountIdentifier = " replay-account " }, default)).Version);
        await Assert.ThrowsAsync<ConflictException>(() => ordering.SetRenewalTargetAsync(renewal.OrderId, renewal.Id,
            set with { AccountIdentifier = "other-account" }, default));
        var claim = new ClaimRenewal(ready.Version, Guid.NewGuid());
        var claimed = await ordering.ClaimRenewalAsync(renewal.Id, claim, default);
        var start = new StartRenewal(claimed.Renewal.Version, Guid.NewGuid(), claimed.ClaimToken!.Value);
        var started = await ordering.StartRenewalAsync(renewal.Id, start, default);
        var replay = await ordering.ClaimRenewalAsync(renewal.Id, claim with { Version = "stale" }, default);
        Assert.Equal(RenewalStatus.PendingVerification, replay.Renewal.Status);
        Assert.Null(replay.ClaimToken);
        using var otherScope = fixture.Scope(Guid.NewGuid());
        await Assert.ThrowsAsync<ConflictException>(() => Ordering(otherScope).ClaimRenewalAsync(renewal.Id, claim, default));
        await Assert.ThrowsAsync<ForbiddenException>(() => Ordering(scope, deniedPermission: "Permissions.Renewal.Process")
            .StartRenewalAsync(renewal.Id, start, default));
        var another = await PaidRenewal(scope, product);
        await Assert.ThrowsAsync<ConflictException>(() => ordering.ClaimRenewalAsync(another.Id, claim, default));
        Assert.Single(started.Attempts);
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task VerificationRequiresSeparatePermissionAndExplicitNotAppliedAllowsANewAttemptWithoutChangingTarget(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var product = await Product(scope, "renewal-not-applied");
        var ordering = Ordering(scope);
        var renewal = await PaidRenewal(scope, product);
        renewal = await ordering.SetRenewalTargetAsync(renewal.OrderId, renewal.Id,
            new(renewal.Version, Guid.NewGuid(), "locked-account"), default);
        var pending = await Start(ordering, renewal);
        var verification = new VerifyRenewal(pending.Renewal.Version, Guid.NewGuid(), pending.Attempts.Single().Id,
            false, null, "Verified on external account: the earlier action had no effect.", "operator-ticket-217");
        await Assert.ThrowsAsync<ForbiddenException>(() => Ordering(scope, deniedPermission: "Permissions.Renewal.Verify")
            .VerifyRenewalAsync(renewal.Id, verification, default));
        using var verifier = fixture.Scope(Guid.NewGuid());
        var verified = await Ordering(verifier).VerifyRenewalAsync(renewal.Id, verification, default);
        Assert.Equal(RenewalStatus.Ready, verified.Renewal.Status);
        Assert.Equal(RenewalOutcome.NotApplied, Assert.Single(verified.Attempts).Outcome);
        Assert.Null(verified.Renewal.VerifiedExpiresAtUtc);
        await RejectTransition(() => ordering.SetRenewalTargetAsync(renewal.OrderId, renewal.Id,
            new(verified.Renewal.Version, Guid.NewGuid(), "replacement-account"), default));
        var retry = await Start(ordering, verified.Renewal);
        Assert.Equal(2, retry.Attempts.Count);
        Assert.Single(retry.Attempts.Where(a => a.Outcome == RenewalOutcome.Pending));
        Assert.Single(retry.Attempts.Where(a => a.Outcome == RenewalOutcome.NotApplied));
        Assert.NotEqual(pending.Attempts.Single().Id, retry.Attempts.Single(a => a.Outcome == RenewalOutcome.Pending).Id);
        Assert.Equal(OrderFulfillmentStatus.InProgress, (await ordering.GetOrderAsync(renewal.OrderId, false, default)).FulfillmentStatus);
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task AppliedVerificationRecordsTheActualExpiryCompletesManualOnlyOrderAndKeepsEvidencePrivate(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var product = await Product(scope, "renewal-applied");
        var ordering = Ordering(scope);
        var renewal = await PaidRenewal(scope, product, 2);
        renewal = await ordering.SetRenewalTargetAsync(renewal.OrderId, renewal.Id,
            new(renewal.Version, Guid.NewGuid(), "applied-account"), default);
        var pending = await Start(ordering, renewal);
        var future = DateTime.UtcNow.AddDays(37);
        var expiry = new DateTime(future.Ticks - future.Ticks % 10, DateTimeKind.Utc);
        const string note = "VerifiedActualExpiry_91287";
        const string evidence = "EvidencePrivateTicket_23746";
        var request = new VerifyRenewal(pending.Renewal.Version, Guid.NewGuid(), pending.Attempts.Single().Id,
            true, expiry, note, evidence);
        await Assert.ThrowsAsync<DomainException>(() => ordering.VerifyRenewalAsync(renewal.Id,
            request with { NewExpiresAtUtc = null }, default));
        await Assert.ThrowsAsync<DomainException>(() => ordering.VerifyRenewalAsync(renewal.Id,
            request with { NewExpiresAtUtc = DateTime.UtcNow.AddDays(-1) }, default));
        await Assert.ThrowsAsync<DomainException>(() => ordering.VerifyRenewalAsync(renewal.Id,
            request with { NewExpiresAtUtc = DateTime.SpecifyKind(expiry, DateTimeKind.Unspecified) }, default));
        var complete = await ordering.VerifyRenewalAsync(renewal.Id, request, default);
        Assert.Equal(RenewalStatus.Completed, complete.Renewal.Status);
        Assert.Equal(expiry, complete.Renewal.VerifiedExpiresAtUtc);
        Assert.Equal(DateTimeKind.Utc, complete.Renewal.VerifiedExpiresAtUtc!.Value.Kind);
        Assert.Equal(DateTimeKind.Utc, complete.Renewal.CompletedAtUtc!.Value.Kind);
        var order = await ordering.GetOrderAsync(renewal.OrderId, false, default);
        Assert.Equal(OrderStatus.Completed, order.Status);
        Assert.Equal(OrderFulfillmentStatus.Completed, order.FulfillmentStatus);
        Assert.False((await db.Orders.AsNoTracking().SingleAsync()).PhysicalDispatchStarted);
        var replay = await ordering.VerifyRenewalAsync(renewal.Id, request with { Version = "stale" }, default);
        Assert.Equal(complete.Renewal.Version, replay.Renewal.Version);
        await Assert.ThrowsAsync<ConflictException>(() => ordering.VerifyRenewalAsync(renewal.Id,
            request with { Note = "Different verification" }, default));
        var stored = await db.Set<RenewalAttempt>().AsNoTracking().SingleAsync();
        var metadata = JsonSerializer.Serialize(new { complete, order });
        Assert.DoesNotContain(note, metadata);
        Assert.DoesNotContain(evidence, metadata);
        Assert.DoesNotContain(stored.ProtectedEvidence!, metadata);
        var before = await db.Trails.CountAsync(t => t.Type == TrailType.Read);
        var content = await ordering.RevealRenewalEvidenceAsync(renewal.Id, stored.Id, default);
        Assert.Contains(note, content.Content);
        Assert.Contains(evidence, content.Content);
        Assert.Equal(before + 1, await db.Trails.CountAsync(t => t.Type == TrailType.Read));
        await Assert.ThrowsAsync<ForbiddenException>(() => Ordering(scope, deniedPermission: "Permissions.Renewal.Verify")
            .RevealRenewalEvidenceAsync(renewal.Id, stored.Id, default));
        var trails = JsonSerializer.Serialize(await db.Trails.AsNoTracking().ToListAsync());
        Assert.DoesNotContain(note, trails);
        Assert.DoesNotContain(evidence, trails);
        Assert.DoesNotContain(stored.ProtectedEvidence!, trails);
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task MaximumVietnameseVerificationNoteAndEvidenceRoundTripWithoutOverflowOrAuditDisclosure(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var product = await Product(scope, "renewal-unicode-evidence");
        var ordering = Ordering(scope);
        var renewal = await PaidRenewal(scope, product);
        renewal = await ordering.SetRenewalTargetAsync(renewal.OrderId, renewal.Id,
            new(renewal.Version, Guid.NewGuid(), "unicode-evidence-account"), default);
        var pending = await Start(ordering, renewal);
        var note = new string('ớ', 1200);
        var evidence = new string('ộ', 1800);
        var completed = await ordering.VerifyRenewalAsync(renewal.Id,
            new(pending.Renewal.Version, Guid.NewGuid(), pending.Attempts.Single().Id, true,
                DateTime.UtcNow.AddDays(30), note, evidence), default);
        Assert.Equal(RenewalStatus.Completed, completed.Renewal.Status);
        var content = await ordering.RevealRenewalEvidenceAsync(renewal.Id, pending.Attempts.Single().Id, default);
        using var document = JsonDocument.Parse(content.Content);
        Assert.Equal(note, document.RootElement.GetProperty("Note").GetString());
        Assert.Equal(evidence, document.RootElement.GetProperty("Evidence").GetString());
        var protectedEvidence = (await db.Set<RenewalAttempt>().AsNoTracking().SingleAsync()).ProtectedEvidence!;
        Assert.True(protectedEvidence.Length > 8000, "Exercise the formerly insufficient encrypted evidence capacity.");
        Assert.True(protectedEvidence.Length <= 32000);
        var audit = JsonSerializer.Serialize(await db.Trails.AsNoTracking().ToListAsync());
        Assert.DoesNotContain(protectedEvidence, audit);
        Assert.DoesNotContain("ProtectedEvidence", audit);
        Assert.DoesNotContain(note, audit);
        Assert.DoesNotContain(evidence, audit);
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql", true), InlineData("postgresql", true)]
    [InlineData("mssql", false), InlineData("postgresql", false)]
    public async Task MixedOrderCompletesOnlyAfterBothPhysicalDeliveryAndVerifiedRenewal(string provider, bool renewalFirst)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var physical = await Product(scope, "renewal-mixed-physical", ProductFulfillmentKind.Physical);
        var extension = await Product(scope, "renewal-mixed-package");
        var digital = await Product(scope, "renewal-mixed-digital", ProductFulfillmentKind.DigitalUnique);
        var catalog = Catalog(scope);
        var balance = Assert.Single((await catalog.InventoryAsync(1, 100, default)).Items
            .Where(i => i.VariantId == physical.Variants.Single().Id));
        await catalog.AdjustAsync(new(balance.Id, balance.Version, Guid.NewGuid(), 1, "Mixed renewal shipping stock"), default);
        await catalog.ImportDigitalAsync(new(digital.Id, digital.Variants.Single().Id, digital.Version,
            [new("CODE", "MixedRenewalCodeSecret")]), default);
        var ordering = Ordering(scope);
        var order = await Checkout(ordering, (physical.Variants.Single().Id, 1), (extension.Variants.Single().Id, 2),
            (digital.Variants.Single().Id, 1));
        order = await ordering.ConfirmPaymentAsync(order.Id, Receipt(order), default);
        var delivery = Assert.Single(await ordering.DigitalDeliveriesAsync(order.Id, default));
        var renewal = Assert.Single(await ordering.RenewalsAsync(order.Id, default));
        renewal = await ordering.SetRenewalTargetAsync(order.Id, renewal.Id,
            new(renewal.Version, Guid.NewGuid(), "mixed-account"), default);
        var pending = await Start(ordering, renewal);
        async Task Verify()
        {
            await ordering.VerifyRenewalAsync(renewal.Id,
                new(pending.Renewal.Version, Guid.NewGuid(), pending.Attempts.Single().Id, true,
                    DateTime.UtcNow.AddDays(45), "External account checked and new expiry confirmed.", null), default);
        }
        async Task Ship()
        {
            var current = await ordering.GetOrderAsync(order.Id, false, default);
            var item = current.Items.Single(i => i.FulfillmentKind == ProductFulfillmentKind.Physical);
            var shipment = await ordering.DispatchShipmentAsync(order.Id,
                new(current.Version, Guid.NewGuid(), "Manual", "RENEWAL-MIXED", [new(item.Id, 1)]), default);
            await ordering.DeliverShipmentAsync(order.Id, shipment.Id,
                new(shipment.OrderVersion, shipment.Version, Guid.NewGuid()), default);
        }
        if (renewalFirst)
        {
            await Verify();
            Assert.False((await db.Orders.AsNoTracking().SingleAsync()).PhysicalDispatchStarted);
        }
        else
            await Ship();
        Assert.Equal(OrderFulfillmentStatus.InProgress, (await ordering.GetOrderAsync(order.Id, false, default)).FulfillmentStatus);
        if (renewalFirst) await Ship(); else await Verify();
        var completed = await ordering.GetOrderAsync(order.Id, false, default);
        Assert.Equal(OrderStatus.Completed, completed.Status);
        Assert.Equal(OrderFulfillmentStatus.Completed, completed.FulfillmentStatus);
        Assert.True((await db.Orders.AsNoTracking().SingleAsync()).PhysicalDispatchStarted);
        Assert.Equal(delivery.Id, Assert.Single(await ordering.DigitalDeliveriesAsync(order.Id, default)).Id);
        Assert.Single(await db.DigitalDeliveries.AsNoTracking().ToListAsync());
        Assert.Single(await db.Set<RenewalRequest>().AsNoTracking().ToListAsync());
        Assert.Single(await db.Set<RenewalAttempt>().AsNoTracking().ToListAsync());
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task ReceiptCommitFailureRollsBackRenewalCreationPaymentAndAuditThenExactRetryCreatesOneRequest(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var product = await Product(scope, "renewal-receipt-rollback");
        var ordering = Ordering(scope);
        var order = await Checkout(ordering, (product.Variants.Single().Id, 2));
        var receipt = Receipt(order);
        var auditBefore = await db.Trails.CountAsync();
        var failure = new CommitFailure();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>(scope.ServiceProvider.GetRequiredService<DbContextOptions<ApplicationDbContext>>())
            .AddInterceptors(failure).Options;
        await using (var failingDb = new ApplicationDbContext(options,
            scope.ServiceProvider.GetRequiredService<ICurrentUser>(), scope.ServiceProvider.GetRequiredService<ISerializerService>(),
            scope.ServiceProvider.GetRequiredService<IEventPublisher>()))
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Ordering(scope, dbOverride: failingDb).ConfirmPaymentAsync(order.Id, receipt, default));
            Assert.Same(failure.Error, error);
            Assert.True(failure.WasReached);
            Assert.Empty(failingDb.ChangeTracker.Entries());
        }
        Assert.Empty(await db.RenewalRequests.AsNoTracking().ToListAsync());
        Assert.Empty(await db.RenewalOperations.AsNoTracking().ToListAsync());
        Assert.Empty(await db.PaymentReceipts.AsNoTracking().ToListAsync());
        Assert.Equal(auditBefore, await db.Trails.CountAsync());
        var unchanged = await ordering.GetOrderAsync(order.Id, false, default);
        Assert.Equal(order.Version, unchanged.Version);
        Assert.Equal(OrderPaymentStatus.Unpaid, unchanged.PaymentStatus);
        Assert.Equal(OrderFulfillmentStatus.Pending, unchanged.FulfillmentStatus);
        await ordering.ConfirmPaymentAsync(order.Id, receipt, default);
        var renewal = Assert.Single(await ordering.RenewalsAsync(order.Id, default));
        Assert.Equal(2, renewal.Quantity);
        Assert.Equal(RenewalStatus.AwaitingTarget, renewal.Status);
        await ordering.ConfirmPaymentAsync(order.Id, receipt, default);
        Assert.Single(await db.RenewalRequests.AsNoTracking().ToListAsync());
        Assert.Single(await db.PaymentReceipts.AsNoTracking().ToListAsync());
        Assert.Equal("Created", Assert.Single(await db.RenewalOperations.AsNoTracking().ToListAsync()).Action);
        Assert.Empty(await db.RenewalAttempts.AsNoTracking().ToListAsync());
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task PaidStockReviewAllowsOwnerTargetButBlocksRenewalWorkUntilAllStockIsRecovered(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        var clock = new MutableClock();
        using var scope = fixture.Scope(Guid.NewGuid());
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var extension = await Product(scope, "renewal-review-package");
        var unique = await Product(scope, "renewal-review-code", ProductFulfillmentKind.DigitalUnique);
        await Catalog(scope).ImportDigitalAsync(new(unique.Id, unique.Variants.Single().Id, unique.Version,
            [new("CODE", "StockReviewRenewalCodeSecret")]), default);
        var ordering = Ordering(scope, clock);
        var order = await Checkout(ordering, (extension.Variants.Single().Id, 2), (unique.Variants.Single().Id, 1));
        clock.Advance(TimeSpan.FromMinutes(35));
        Assert.Equal(1, await ordering.ExpireAsync(default));
        using var otherScope = fixture.Scope(Guid.NewGuid());
        var competing = Ordering(otherScope, clock);
        var reserved = await Checkout(competing, (unique.Variants.Single().Id, 1));
        var expired = await ordering.GetOrderAsync(order.Id, false, default);
        var paid = await ordering.ConfirmPaymentAsync(order.Id, Receipt(expired, clock), default);
        Assert.Equal(OrderPaymentStatus.Paid, paid.PaymentStatus);
        Assert.Equal(OrderFulfillmentStatus.NeedsReview, paid.FulfillmentStatus);
        var renewal = Assert.Single(await ordering.RenewalsAsync(order.Id, default));
        renewal = await ordering.SetRenewalTargetAsync(order.Id, renewal.Id,
            new(renewal.Version, Guid.NewGuid(), "review-customer-account"), default);
        Assert.Equal(RenewalStatus.Ready, renewal.Status);
        await Assert.ThrowsAsync<ConflictException>(() => ordering.ClaimRenewalAsync(renewal.Id,
            new(renewal.Version, Guid.NewGuid()), default));
        await Assert.ThrowsAsync<ConflictException>(() => ordering.StartRenewalAsync(renewal.Id,
            new(renewal.Version, Guid.NewGuid(), Guid.NewGuid()), default));
        Assert.Empty(await db.RenewalAttempts.AsNoTracking().ToListAsync());
        Assert.Empty(await db.DigitalDeliveries.AsNoTracking().ToListAsync());
        await competing.CancelAsync(reserved.Id, new(reserved.Version), false, default);
        var recovered = await ordering.RetryFulfillmentAsync(paid.Id, new(paid.Version), default);
        Assert.Equal(OrderFulfillmentStatus.InProgress, recovered.FulfillmentStatus);
        Assert.Equal(OrderStatus.AwaitingProcessing, recovered.Status);
        Assert.Single(await db.DigitalDeliveries.AsNoTracking().ToListAsync());
        var claimed = await ordering.ClaimRenewalAsync(renewal.Id, new(renewal.Version, Guid.NewGuid()), default);
        Assert.Equal(RenewalStatus.Claimed, claimed.Renewal.Status);
        Assert.NotNull(claimed.ClaimToken);
        Assert.Single(await db.RenewalRequests.AsNoTracking().ToListAsync());
        Assert.Single((await ordering.RenewalHistoryAsync(renewal.Id, default)).Where(h => h.Action == "Created"));
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task ExplicitReleaseRequiresCurrentHolderTokenVersionAndLeaseAndReplayCannotReleaseANewClaim(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        var clock = new MutableClock();
        var firstActor = Guid.NewGuid();
        using var scope = fixture.Scope(firstActor);
        var product = await Product(scope, "renewal-explicit-release");
        var ordering = Ordering(scope, clock);
        var renewal = await PaidRenewal(scope, product, clock: clock);
        renewal = await ordering.SetRenewalTargetAsync(renewal.OrderId, renewal.Id,
            new(renewal.Version, Guid.NewGuid(), "release-customer-account"), default);
        var claimed = await ordering.ClaimRenewalAsync(renewal.Id, new(renewal.Version, Guid.NewGuid()), default);
        using var otherScope = fixture.Scope(Guid.NewGuid());
        var other = Ordering(otherScope, clock);
        await RejectTransition(() => other.ReleaseRenewalAsync(renewal.Id,
            new(claimed.Renewal.Version, Guid.NewGuid(), claimed.ClaimToken!.Value), default));
        await RejectTransition(() => ordering.ReleaseRenewalAsync(renewal.Id,
            new(claimed.Renewal.Version, Guid.NewGuid(), Guid.NewGuid()), default));
        await Assert.ThrowsAsync<ConflictException>(() => ordering.ReleaseRenewalAsync(renewal.Id,
            new(renewal.Version, Guid.NewGuid(), claimed.ClaimToken!.Value), default));
        var release = new ReleaseRenewal(claimed.Renewal.Version, Guid.NewGuid(), claimed.ClaimToken!.Value);
        var ready = await ordering.ReleaseRenewalAsync(renewal.Id, release, default);
        Assert.Equal(RenewalStatus.Ready, ready.Renewal.Status);
        Assert.Null(ready.ClaimedBy);
        Assert.Null(ready.ClaimToken);
        Assert.Null(ready.ClaimUntilUtc);
        Assert.Equal(ready.Renewal.Version, (await ordering.ReleaseRenewalAsync(renewal.Id,
            release with { Version = "stale-after-release", ClaimToken = Guid.NewGuid() }, default)).Renewal.Version);
        await Assert.ThrowsAsync<ForbiddenException>(() => Ordering(scope, clock, "Permissions.Renewal.Process")
            .ReleaseRenewalAsync(renewal.Id, release, default));
        var next = await other.ClaimRenewalAsync(renewal.Id, new(ready.Renewal.Version, Guid.NewGuid()), default);
        var replay = await ordering.ReleaseRenewalAsync(renewal.Id, release, default);
        Assert.Equal(next.Renewal.Version, replay.Renewal.Version);
        Assert.Equal(RenewalStatus.Claimed, replay.Renewal.Status);
        Assert.Equal(next.ClaimedBy, replay.ClaimedBy);
        Assert.Null(replay.ClaimToken);
        clock.Advance(TimeSpan.FromMinutes(16));
        await RejectTransition(() => other.ReleaseRenewalAsync(renewal.Id,
            new(next.Renewal.Version, Guid.NewGuid(), next.ClaimToken!.Value), default));
        var reclaimed = await ordering.ClaimRenewalAsync(renewal.Id, new(next.Renewal.Version, Guid.NewGuid()), default);
        Assert.Equal(firstActor, reclaimed.ClaimedBy);
        Assert.NotEqual(next.ClaimToken, reclaimed.ClaimToken);
        var history = await ordering.RenewalHistoryAsync(renewal.Id, default);
        Assert.Single(history.Where(h => h.Action == "Release"));
        Assert.Single(history.Where(h => h.Action == "ClaimExpired"));
        Assert.Empty(reclaimed.Attempts);
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task CommitFailureRollsBackVerificationEvidenceHistoryAuditAndOrderThenExactOperationCanRetry(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var product = await Product(scope, "renewal-rollback");
        var ordering = Ordering(scope);
        var renewal = await PaidRenewal(scope, product);
        renewal = await ordering.SetRenewalTargetAsync(renewal.OrderId, renewal.Id,
            new(renewal.Version, Guid.NewGuid(), "rollback-account"), default);
        var pending = await Start(ordering, renewal);
        var orderBefore = await ordering.GetOrderAsync(renewal.OrderId, false, default);
        var historyBefore = await db.Set<RenewalOperation>().CountAsync();
        var auditBefore = await db.Trails.CountAsync();
        var request = new VerifyRenewal(pending.Renewal.Version, Guid.NewGuid(), pending.Attempts.Single().Id,
            true, DateTime.UtcNow.AddDays(21), "The account expiry was verified before saving.", "RollbackEvidenceSecret");
        var failure = new CommitFailure();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>(scope.ServiceProvider.GetRequiredService<DbContextOptions<ApplicationDbContext>>())
            .AddInterceptors(failure).Options;
        await using (var failingDb = new ApplicationDbContext(options,
            scope.ServiceProvider.GetRequiredService<ICurrentUser>(), scope.ServiceProvider.GetRequiredService<ISerializerService>(),
            scope.ServiceProvider.GetRequiredService<IEventPublisher>()))
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Ordering(scope, dbOverride: failingDb).VerifyRenewalAsync(renewal.Id, request, default));
            Assert.Same(failure.Error, error);
            Assert.True(failure.WasReached);
            Assert.Empty(failingDb.ChangeTracker.Entries());
        }
        var after = await ordering.GetRenewalAdminAsync(renewal.Id, default);
        Assert.Equal(pending.Renewal.Version, after.Renewal.Version);
        Assert.Equal(RenewalStatus.PendingVerification, after.Renewal.Status);
        Assert.Equal(RenewalOutcome.Pending, Assert.Single(after.Attempts).Outcome);
        Assert.Null((await db.Set<RenewalAttempt>().AsNoTracking().SingleAsync()).ProtectedEvidence);
        Assert.Equal(historyBefore, await db.Set<RenewalOperation>().CountAsync());
        Assert.Equal(auditBefore, await db.Trails.CountAsync());
        var orderAfter = await ordering.GetOrderAsync(renewal.OrderId, false, default);
        Assert.Equal(orderBefore.Version, orderAfter.Version);
        Assert.Equal(OrderFulfillmentStatus.InProgress, orderAfter.FulfillmentStatus);
        await ordering.VerifyRenewalAsync(renewal.Id, request, default);
        Assert.Equal(OrderStatus.Completed, (await ordering.GetOrderAsync(renewal.OrderId, false, default)).Status);
        Assert.Equal(historyBefore + 1, await db.Set<RenewalOperation>().CountAsync());
        Assert.Single(await db.Set<RenewalAttempt>().AsNoTracking().ToListAsync());
        await ordering.VerifyRenewalAsync(renewal.Id, request, default);
        Assert.Equal(historyBefore + 1, await db.Set<RenewalOperation>().CountAsync());
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task ExplicitProvisionRequiresPaymentVersionAndPermissionAndRepairsLegacyOrdersWithoutReadSideEffects(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        var owner = Guid.NewGuid();
        using var scope = fixture.Scope(owner);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var product = await Product(scope, "renewal-legacy");
        var ordering = Ordering(scope);
        var order = await Checkout(ordering, (product.Variants.Single().Id, 2));
        await Assert.ThrowsAsync<ConflictException>(() => ordering.ProvisionRenewalsAsync(order.Id, new(order.Version), default));
        // A domain-seeded paid legacy order predates automatic provisioning; GET must remain read-only.
        db.ChangeTracker.Clear();
        var legacy = await db.Orders.SingleAsync(o => o.Id == order.Id);
        legacy.RecordPayment(DateTime.UtcNow.AddSeconds(-1));
        legacy.CompleteFulfillment(false);
        await db.SaveChangesAsync();
        var version = legacy.Version;
        Assert.Empty(await ordering.RenewalsAsync(order.Id, default));
        Assert.Empty(await db.Set<RenewalRequest>().AsNoTracking().ToListAsync());
        await Assert.ThrowsAsync<ForbiddenException>(() => Ordering(scope, deniedPermission: "Permissions.Renewal.Process")
            .ProvisionRenewalsAsync(order.Id, new(version), default));
        await Assert.ThrowsAsync<ConflictException>(() => ordering.ProvisionRenewalsAsync(order.Id, new("stale"), default));
        var provisioned = Assert.Single(await ordering.ProvisionRenewalsAsync(order.Id, new(version), default));
        Assert.Equal(2, provisioned.Quantity);
        Assert.Equal(RenewalStatus.AwaitingTarget, provisioned.Status);
        Assert.Equal(provisioned.Id, Assert.Single(await ordering.ProvisionRenewalsAsync(order.Id, new(version), default)).Id);
        Assert.Single(await db.Set<RenewalRequest>().AsNoTracking().ToListAsync());
        Assert.Single((await ordering.RenewalHistoryAsync(provisioned.Id, default)).Where(h => h.Action == "Created"));
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task HttpRenewalsEnforceAuthenticationOwnershipSeparateVerifyPermissionAndNoStore(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        var oldEmail = Environment.GetEnvironmentVariable("BOILERATE_BOOTSTRAP_EMAIL");
        var oldPassword = Environment.GetEnvironmentVariable("BOILERATE_BOOTSTRAP_PASSWORD");
        try
        {
            Environment.SetEnvironmentVariable("BOILERATE_BOOTSTRAP_EMAIL", "renewals@example.test");
            Environment.SetEnvironmentVariable("BOILERATE_BOOTSTRAP_PASSWORD", "RenewalPassword!29874");
            using var bootstrap = fixture.Scope();
            await bootstrap.ServiceProvider.GetRequiredService<FoundationBootstrap>().RunAsync(["--bootstrap-admin"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable("BOILERATE_BOOTSTRAP_EMAIL", oldEmail);
            Environment.SetEnvironmentVariable("BOILERATE_BOOTSTRAP_PASSWORD", oldPassword);
        }
        Guid owner;
        ProductDto product;
        using (var setup = fixture.Scope(Guid.NewGuid()))
        {
            var db = setup.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            owner = Guid.Parse(await db.Users.Where(u => u.Email == "renewals@example.test").Select(u => u.Id).SingleAsync());
            product = await Product(setup, "renewal-http");
        }
        RenewalDto mine;
        using (var scope = fixture.Scope(owner))
            mine = await PaidRenewal(scope, product);
        RenewalDto another;
        using (var scope = fixture.Scope(Guid.NewGuid()))
            another = await PaidRenewal(scope, product);
        await using var app = await TestHostApplication.StartAsync(fixture, true);
        using var client = app.GetTestClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/api/orders/{mine.OrderId}/renewals")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync($"/api/admin/renewals/{mine.Id}/claim",
            new ClaimRenewal(mine.Version, Guid.NewGuid()))).StatusCode);
        var login = await client.PostAsJsonAsync("/api/tokens/get", new TokenRequest("renewals@example.test", "RenewalPassword!29874"));
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<TokenResponse>())!;
        client.DefaultRequestHeaders.Authorization = new("Bearer", token.Token);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/orders/{another.OrderId}/renewals")).StatusCode);
        var response = await client.PutAsJsonAsync($"/api/orders/{mine.OrderId}/renewals/{mine.Id}/target",
            new SetRenewalTarget(mine.Version, Guid.NewGuid(), "HttpPrivateRenewalAccount"));
        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.DoesNotContain("HttpPrivateRenewalAccount", await response.Content.ReadAsStringAsync());
        mine = (await response.Content.ReadFromJsonAsync<RenewalDto>())!;
        response = await client.GetAsync($"/api/orders/{mine.OrderId}/renewals/{mine.Id}/target");
        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal("HttpPrivateRenewalAccount", (await response.Content.ReadFromJsonAsync<RenewalContentDto>())!.Content);
        response = await client.PostAsJsonAsync($"/api/admin/renewals/{mine.Id}/claim", new ClaimRenewal(mine.Version, Guid.NewGuid()));
        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.CacheControl?.NoStore);
        var claimed = (await response.Content.ReadFromJsonAsync<RenewalAdminDto>())!;
        response = await client.PostAsJsonAsync($"/api/admin/renewals/{mine.Id}/start",
            new StartRenewal(claimed.Renewal.Version, Guid.NewGuid(), claimed.ClaimToken!.Value));
        response.EnsureSuccessStatusCode();
        var started = (await response.Content.ReadFromJsonAsync<RenewalAdminDto>())!;
        using (var scope = fixture.Scope(owner))
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var function = await db.Functions.Where(f => f.Code == "Renewal").Select(f => f.Id).SingleAsync();
            var action = await db.Actions.Where(a => a.Code == "Verify").Select(a => a.Id).SingleAsync();
            await db.Permissions.Where(p => p.FunctionId == function && p.ActionId == action)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.DeletedOn, DateTime.UtcNow));
        }
        response = await client.PostAsJsonAsync($"/api/admin/renewals/{mine.Id}/verify",
            new VerifyRenewal(started.Renewal.Version, Guid.NewGuid(), started.Attempts.Single().Id, true,
                DateTime.UtcNow.AddDays(30), "Verified via external system", null));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        response = await client.GetAsync($"/api/admin/renewals/{mine.Id}");
        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.DoesNotContain("HttpPrivateRenewalAccount", await response.Content.ReadAsStringAsync());
        Assert.Equal(RenewalStatus.PendingVerification, (await response.Content.ReadFromJsonAsync<RenewalAdminDto>())!.Renewal.Status);
    }
}
