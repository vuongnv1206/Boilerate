using System.Text.Json;
using Ardalis.Specification.EntityFrameworkCore;
using Boilerate.Application.Common.Events;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Models;
using Boilerate.Application.Common.Specification;
using Boilerate.Domain.Auditing;
using Boilerate.Domain.Common.Contracts;
using Boilerate.Domain.Common.Events;
using Boilerate.Domain.Notifications;
using Boilerate.Infrastructure.Auth;
using Boilerate.Infrastructure.Common.Services;
using Boilerate.Infrastructure.Persistence.Context;
using Boilerate.Infrastructure.Persistence.Initialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;
using ActionEntity = Boilerate.Domain.Identity.Action;

namespace Boilerate.Tests;

[Collection("Foundation")]
public sealed class PersistenceMaintenanceTests
{
    [Theory]
    [InlineData("sync-default")]
    [InlineData("sync-true")]
    [InlineData("sync-false")]
    [InlineData("async-false")]
    public async Task UnsupportedSaveEntryPointsFailBeforeModifyingTrackedState(string entryPoint)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=unused.invalid;Database=unused;Integrated Security=true;TrustServerCertificate=true")
            .Options;
        await using var db = new ApplicationDbContext(options, new CurrentUser(), new NewtonSoftService(), Substitute.For<IEventPublisher>());
        var notification = Notification.CreateBroadcast("Pending", "Must not persist", NotificationType.Info);
        db.Notifications.Add(notification);

        switch (entryPoint)
        {
            case "sync-default":
                Assert.Throws<NotSupportedException>(() => db.SaveChanges());
                break;
            case "sync-true":
                Assert.Throws<NotSupportedException>(() => db.SaveChanges(true));
                break;
            case "sync-false":
                Assert.Throws<NotSupportedException>(() => db.SaveChanges(false));
                break;
            case "async-false":
                await Assert.ThrowsAsync<NotSupportedException>(() => db.SaveChangesAsync(false));
                break;
        }

        Assert.Equal(EntityState.Added, db.Entry(notification).State);
        Assert.Empty(db.ChangeTracker.Entries<Trail>());
        Assert.Null(notification.DeletedOn);
    }

    [Theory]
    [Trait("Category", "ProviderIntegration")]
    [InlineData("mssql")]
    [InlineData("postgresql")]
    public async Task BooleanAsyncSaveUsesTheSameAuditSoftDeleteAndEventPipeline(string provider)
    {
        await using var fixture = new FoundationDatabase(provider);
        await fixture.CreateAsync();
        using var scope = fixture.Scope();
        var actorId = Guid.NewGuid();
        var actor = new CurrentUser();
        actor.SetCurrentUserId(actorId.ToString());
        var publisher = Substitute.For<IEventPublisher>();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<ApplicationDbContext>>();
        await using var db = new ApplicationDbContext(options, actor, new NewtonSoftService(), publisher);
        var notification = Notification.CreateBroadcast("Audited", "Saved through bool overload", NotificationType.Info);
        notification.DomainEvents.Add(EntityCreatedEvent.WithEntity(notification));
        db.Notifications.Add(notification);

        await db.SaveChangesAsync(true);
        Assert.Equal(actorId, notification.CreatedBy);
        Assert.True(await db.Trails.AnyAsync(x => x.TableName == "Notifications" && x.UserId == actorId && x.Type == TrailType.Create));

        notification.DomainEvents.Add(EntityDeletedEvent.WithEntity(notification));
        db.Notifications.Remove(notification);
        await db.SaveChangesAsync(true);

        Assert.False(await db.Notifications.AnyAsync());
        Assert.NotNull((await db.Notifications.IgnoreQueryFilters().SingleAsync()).DeletedOn);
        Assert.True(await db.Trails.AnyAsync(x => x.TableName == "Notifications" && x.UserId == actorId && x.Type == TrailType.Delete));
        await publisher.Received(2).PublishAsync(Arg.Any<IEvent>());
        Assert.Null(db.Database.CurrentTransaction);

        var physicallyDeleted = await db.Trails.FirstAsync();
        physicallyDeleted.DomainEvents.Add(EntityDeletedEvent.WithEntity(physicallyDeleted));
        db.Trails.Remove(physicallyDeleted);
        await db.SaveChangesAsync(true);
        Assert.Equal(EntityState.Detached, db.Entry(physicallyDeleted).State);
        await publisher.Received(3).PublishAsync(Arg.Any<IEvent>());
    }

    [Theory]
    [Trait("Category", "ProviderIntegration")]
    [InlineData("mssql")]
    [InlineData("postgresql")]
    public async Task ExternalCommitDispatchesOnlySavedEventsAndKeepsLaterUnsavedEvents(string provider)
    {
        await using var fixture = new FoundationDatabase(provider);
        await fixture.CreateAsync();
        using var scope = fixture.Scope();
        var publisher = Substitute.For<IEventPublisher>();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<ApplicationDbContext>>();
        await using var db = new ApplicationDbContext(options, new CurrentUser(), new NewtonSoftService(), publisher);
        var notification = Notification.CreateBroadcast("External", "Committed batch", NotificationType.Info);
        var savedEvent = EntityCreatedEvent.WithEntity(notification);
        var laterEvent = EntityUpdatedEvent.WithEntity(notification);
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            notification.DomainEvents.Add(savedEvent);
            db.Notifications.Add(notification);
            await db.SaveChangesAsync();
            notification.DomainEvents.Add(laterEvent);
            await publisher.DidNotReceive().PublishAsync(Arg.Any<IEvent>());
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.DispatchCommittedEventsAsync());
            await transaction.CommitAsync();
        }

        await db.DispatchCommittedEventsAsync();
        await publisher.Received(1).PublishAsync(savedEvent);
        await publisher.DidNotReceive().PublishAsync(laterEvent);
        Assert.Same(laterEvent, Assert.Single(notification.DomainEvents));

        await db.SaveChangesAsync();
        await publisher.Received(1).PublishAsync(laterEvent);
    }

    [Theory]
    [Trait("Category", "ProviderIntegration")]
    [InlineData("mssql")]
    [InlineData("postgresql")]
    public async Task ExternalRollbackCannotAutomaticallyLeakItsEventsIntoAnotherSave(string provider)
    {
        await using var fixture = new FoundationDatabase(provider);
        await fixture.CreateAsync();
        using var scope = fixture.Scope();
        var publisher = Substitute.For<IEventPublisher>();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<ApplicationDbContext>>();
        await using var db = new ApplicationDbContext(options, new CurrentUser(), new NewtonSoftService(), publisher);
        var rolledBack = Notification.CreateBroadcast("Rollback", "Not committed", NotificationType.Info);
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            rolledBack.DomainEvents.Add(EntityCreatedEvent.WithEntity(rolledBack));
            db.Notifications.Add(rolledBack);
            await db.SaveChangesAsync();
            await transaction.RollbackAsync();
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.DiscardPendingEvents();
        db.ChangeTracker.Clear();
        var committed = Notification.CreateBroadcast("Commit", "New batch", NotificationType.Info);
        var committedEvent = EntityCreatedEvent.WithEntity(committed);
        committed.DomainEvents.Add(committedEvent);
        db.Notifications.Add(committed);
        await db.SaveChangesAsync();

        await publisher.Received(1).PublishAsync(committedEvent);
        await publisher.Received(1).PublishAsync(Arg.Any<IEvent>());
        Assert.Equal(committed.Id, (await db.Notifications.SingleAsync()).Id);
    }

    [Theory]
    [Trait("Category", "ProviderIntegration")]
    [InlineData("mssql")]
    [InlineData("postgresql")]
    public async Task FailedEventIsNotAutomaticallyRetriedButUnattemptedEventsRemainAvailable(string provider)
    {
        await using var fixture = new FoundationDatabase(provider);
        await fixture.CreateAsync();
        using var scope = fixture.Scope();
        var publisher = Substitute.For<IEventPublisher>();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<ApplicationDbContext>>();
        await using var db = new ApplicationDbContext(options, new CurrentUser(), new NewtonSoftService(), publisher);
        var notification = Notification.CreateBroadcast("Events", "Committed before handler failure", NotificationType.Info);
        var first = EntityCreatedEvent.WithEntity(notification);
        var second = EntityUpdatedEvent.WithEntity(notification);
        notification.DomainEvents.Add(first);
        notification.DomainEvents.Add(second);
        db.Notifications.Add(notification);
        publisher.PublishAsync(first).Returns(Task.FromException(new InvalidOperationException("Handler failed after commit")));

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        using (var observer = fixture.Scope())
        {
            Assert.True(await observer.ServiceProvider.GetRequiredService<ApplicationDbContext>().Notifications.AnyAsync(x => x.Id == notification.Id));
        }

        await publisher.Received(1).PublishAsync(first);
        await publisher.DidNotReceive().PublishAsync(second);
        await db.DispatchCommittedEventsAsync();
        await publisher.Received(1).PublishAsync(first);
        await publisher.Received(1).PublishAsync(second);
    }

    [Fact]
    public void MissingPaginationFilterStillAppliesTheDefaultPageAndOrdering()
    {
        var rows = Enumerable.Range(0, 20).Select(_ => new Trail()).Reverse().ToArray();
        var result = new EntitiesByPaginationFilterSpec<Trail>(null).Evaluate(rows).ToArray();
        Assert.Equal(10, result.Length);
        Assert.Equal(rows.OrderBy(row => row.Id).Take(10).Select(row => row.Id), result.Select(row => row.Id));
    }

    [Theory]
    [Trait("Category", "ProviderIntegration")]
    [InlineData("mssql")]
    [InlineData("postgresql")]
    public async Task NonUniqueSortUsesIdToKeepPageBoundariesStable(string provider)
    {
        await using var fixture = new FoundationDatabase(provider);
        await fixture.CreateAsync();
        using var scope = fixture.Scope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var timestamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        db.Trails.AddRange(Enumerable.Range(0, 4).Select(index => new Trail
        {
            DateTime = timestamp,
            TableName = "PagingProbe",
            PrimaryKey = index.ToString(),
            Type = TrailType.Create
        }));
        await db.SaveChangesAsync();

        var expected = await db.Trails.OrderBy(row => row.Id).Select(row => row.Id).ToArrayAsync();
        var pages = new List<Guid>();
        for (int page = 1; page <= 2; page++)
        {
            var specification = new EntitiesByPaginationFilterSpec<Trail>(new PaginationFilter
            {
                PageNumber = page,
                PageSize = 2,
                OrderBy = new[] { "DateTime desc" }
            });
            pages.AddRange(await db.Trails.AsNoTracking().WithSpecification(specification).Select(row => row.Id).ToListAsync());
        }

        Assert.Equal(expected, pages);
        Assert.Equal(4, pages.Distinct().Count());
    }

    [Theory]
    [Trait("Category", "ProviderIntegration")]
    [InlineData("mssql")]
    [InlineData("postgresql")]
    public async Task CurrentBaselinePreflightUsesCodesAndReportsCorruptionWithoutRepairingIt(string provider)
    {
        await using var fixture = new FoundationDatabase(provider);
        await fixture.CreateAsync();
        using var scope = fixture.Scope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var action = new ActionEntity("View") { Name = "Xem dữ liệu" };
        db.Actions.Add(action);
        await db.SaveChangesAsync();
        var bootstrap = scope.ServiceProvider.GetRequiredService<FoundationBootstrap>();
        TextWriter previousOutput = Console.Out;
        using var output = new StringWriter();
        try
        {
            Console.SetOut(output);
            await bootstrap.PreflightAsync();
            using (JsonDocument report = JsonDocument.Parse(output.ToString()))
            {
                Assert.True(report.RootElement.GetProperty("Actions").GetProperty("IsValid").GetBoolean());
            }

            await db.Actions.Where(x => x.Id == action.Id).ExecuteUpdateAsync(update => update.SetProperty(x => x.NormalizedCode, "BROKEN"));
            output.GetStringBuilder().Clear();
            await Assert.ThrowsAsync<ConflictException>(() => bootstrap.PreflightAsync());
            using JsonDocument invalidReport = JsonDocument.Parse(output.ToString());
            Assert.Equal(action.Id, invalidReport.RootElement.GetProperty("Actions").GetProperty("InvalidNormalizedCodeIds")[0].GetGuid());
            Assert.Equal("BROKEN", await db.Actions.AsNoTracking().Select(x => x.NormalizedCode).SingleAsync());
            Assert.Empty(await db.Permissions.IgnoreQueryFilters().ToListAsync());
        }
        finally
        {
            Console.SetOut(previousOutput);
        }
    }
}
