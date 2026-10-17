using Boilerate.Application.Common.Events;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Contracts;
using Boilerate.Domain.Common.Events;
using Boilerate.Infrastructure.Auth;
using Boilerate.Infrastructure.Commerce;
using Boilerate.Infrastructure.Common.Services;
using Boilerate.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Boilerate.Tests;

[Collection("Foundation")]
public sealed class CommerceUnitOfWorkTests
{
    [Fact]
    public async Task PendingChangesAreRejectedBeforeTheyCanBeSilentlyDetached()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=unused.invalid;Database=unused;Integrated Security=true;TrustServerCertificate=true").Options;
        await using var db = new ApplicationDbContext(options, new CurrentUser(), new NewtonSoftService(), Substitute.For<IEventPublisher>());
        var pending = new Category("Pending", null, "pending");
        db.Categories.Add(pending);

        await Assert.ThrowsAsync<InvalidOperationException>(() => new CommerceUnitOfWork(db).ExecuteAsync(() => Task.FromResult(true), default));

        Assert.Equal(EntityState.Added, db.Entry(pending).State);
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task CommitDisposesTransactionBeforePublishingAndAllowsAnotherCommand(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope();
        var publisher = Substitute.For<IEventPublisher>();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<ApplicationDbContext>>();
        await using var db = new ApplicationDbContext(options, new CurrentUser(), new NewtonSoftService(), publisher);
        var work = new CommerceUnitOfWork(db);
        var category = new Category("Committed", null, "committed");
        var committedEvent = EntityCreatedEvent.WithEntity(category);
        publisher.PublishAsync(committedEvent).Returns(async _ =>
        {
            Assert.Null(db.Database.CurrentTransaction);
            using var observer = fixture.Scope();
            Assert.True(await observer.ServiceProvider.GetRequiredService<ApplicationDbContext>().Categories.AnyAsync(c => c.Id == category.Id));
            await work.ExecuteAsync(() =>
            {
                db.Categories.Add(new Category("Handler", null, "handler"));
                return Task.FromResult(true);
            }, default);
        });

        await work.ExecuteAsync(() =>
        {
            db.Categories.Add(category);
            category.DomainEvents.Add(committedEvent);
            return Task.FromResult(category.Id);
        }, default);

        await publisher.Received(1).PublishAsync(committedEvent);
        Assert.Equal(2, await db.Categories.CountAsync());
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task RollbackDiscardsSavedEventsAndPostCommitHandlerFailureDoesNotRollback(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope();
        var publisher = Substitute.For<IEventPublisher>();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<ApplicationDbContext>>();
        await using var db = new ApplicationDbContext(options, new CurrentUser(), new NewtonSoftService(), publisher);
        var work = new CommerceUnitOfWork(db);
        var rolledBack = new Category("Rolled back", null, "rolled-back");
        var rolledBackEvent = EntityCreatedEvent.WithEntity(rolledBack);
        await Assert.ThrowsAsync<InvalidOperationException>(() => work.ExecuteAsync<bool>(async () =>
        {
            db.Categories.Add(rolledBack);
            rolledBack.DomainEvents.Add(rolledBackEvent);
            await db.SaveChangesAsync();
            throw new InvalidOperationException("Abort before commit");
        }, default));
        await publisher.DidNotReceive().PublishAsync(Arg.Any<IEvent>());
        Assert.False(await db.Categories.AnyAsync());

        var committed = new Category("Committed", null, "committed");
        var committedEvent = EntityCreatedEvent.WithEntity(committed);
        publisher.PublishAsync(committedEvent).Returns(Task.FromException(new InvalidOperationException("Handler failed after commit")));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => work.ExecuteAsync(() =>
        {
            db.Categories.Add(committed);
            committed.DomainEvents.Add(committedEvent);
            return Task.FromResult(committed.Id);
        }, default));
        Assert.Equal("Handler failed after commit", error.Message);
        Assert.True(await db.Categories.AnyAsync(c => c.Id == committed.Id));
        await work.ExecuteAsync(() =>
        {
            db.Categories.Add(new Category("Next", null, "next"));
            return Task.FromResult(true);
        }, default);
        await publisher.DidNotReceive().PublishAsync(rolledBackEvent);
        await publisher.Received(1).PublishAsync(committedEvent);
        Assert.Equal(2, await db.Categories.CountAsync());
        Assert.Null(db.Database.CurrentTransaction);
    }
}
