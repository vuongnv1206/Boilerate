using Ardalis.Specification;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Identity.Users;
using Boilerate.Domain.Auditing;
using Boilerate.Domain.Common.Events;
using Boilerate.Domain.Identity;
using Boilerate.Infrastructure.Persistence.Context;
using Boilerate.Infrastructure.Persistence.Repository;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Boilerate.Tests;

[Collection("Foundation")]
public sealed class MaintainabilityRegressionTests
{
    [Theory]
    [InlineData("add")]
    [InlineData("update")]
    [InlineData("delete")]
    public async Task RangeWritesRaiseEventsOnTheInstancesPassedToPersistence(string operation)
    {
        var repository = Substitute.For<IRepository<Trail>>();
        var decorator = new EventAddingRepositoryDecorator<Trail>(repository);
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        var generated = new List<Trail>();
        List<Trail>? persisted = null;
        int enumerations = 0;

        IEnumerable<Trail> Records()
        {
            if (++enumerations != 1)
            {
                throw new InvalidOperationException("The input may be consumed only once.");
            }

            for (int index = 0; index < 2; index++)
            {
                var entity = new Trail();
                generated.Add(entity);
                yield return entity;
            }
        }

        repository.AddRangeAsync(Arg.Any<IEnumerable<Trail>>(), token).Returns(call =>
        {
            persisted = call.Arg<IEnumerable<Trail>>().ToList();
            return Task.FromResult<IEnumerable<Trail>>(persisted);
        });
        repository.UpdateRangeAsync(Arg.Any<IEnumerable<Trail>>(), token).Returns(call =>
        {
            persisted = call.Arg<IEnumerable<Trail>>().ToList();
            return Task.CompletedTask;
        });
        repository.DeleteRangeAsync(Arg.Any<IEnumerable<Trail>>(), token).Returns(call =>
        {
            persisted = call.Arg<IEnumerable<Trail>>().ToList();
            return Task.CompletedTask;
        });

        switch (operation)
        {
            case "add":
                await decorator.AddRangeAsync(Records(), token);
                break;
            case "update":
                await decorator.UpdateRangeAsync(Records(), token);
                break;
            case "delete":
                await decorator.DeleteRangeAsync(Records(), token);
                break;
        }

        Assert.Equal(1, enumerations);
        Assert.NotNull(persisted);
        Assert.Equal(2, persisted.Count);
        for (int index = 0; index < persisted.Count; index++)
        {
            var entity = persisted[index];
            Assert.Same(generated[index], entity);
            var domainEvent = Assert.Single(entity.DomainEvents);
            switch (operation)
            {
                case "add":
                    Assert.Same(entity, Assert.IsType<EntityCreatedEvent<Trail>>(domainEvent).Entity);
                    break;
                case "update":
                    Assert.Same(entity, Assert.IsType<EntityUpdatedEvent<Trail>>(domainEvent).Entity);
                    break;
                case "delete":
                    Assert.Same(entity, Assert.IsType<EntityDeletedEvent<Trail>>(domainEvent).Entity);
                    break;
            }
        }
    }

    [Fact]
    public async Task SpecificationDeletesRaiseEventsForExactlyTheSelectedEntities()
    {
        var repository = Substitute.For<IRepository<Trail>>();
        var decorator = new EventAddingRepositoryDecorator<Trail>(repository);
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        var specification = new AllTrailsSpecification();
        var selected = new List<Trail> { new(), new() };
        repository.ListAsync(specification, token).Returns(selected);
        List<Trail>? deleted = null;
        repository.DeleteRangeAsync(Arg.Any<IEnumerable<Trail>>(), token).Returns(call =>
        {
            deleted = call.Arg<IEnumerable<Trail>>().ToList();
            return Task.CompletedTask;
        });

        await decorator.DeleteRangeAsync(specification, token);

        await repository.Received(1).ListAsync(specification, token);
        await repository.DidNotReceive().DeleteRangeAsync(Arg.Any<ISpecification<Trail>>(), Arg.Any<CancellationToken>());
        Assert.NotNull(deleted);
        Assert.Equal(selected, deleted);
        foreach (var entity in deleted)
        {
            Assert.Same(entity, Assert.IsType<EntityDeletedEvent<Trail>>(Assert.Single(entity.DomainEvents)).Entity);
        }
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql")]
    [InlineData("postgresql")]
    public async Task UserSearchAppliesActiveStatusToBothPageAndTotalCount(string provider)
    {
        await using var fixture = new FoundationDatabase(provider);
        await fixture.CreateAsync();
        using var scope = fixture.Scope();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        database.Users.AddRange(
            new ApplicationUser { UserName = "match-active-a", IsActive = true },
            new ApplicationUser { UserName = "match-active-b", IsActive = true },
            new ApplicationUser { UserName = "match-inactive-a", IsActive = false },
            new ApplicationUser { UserName = "match-inactive-b", IsActive = false },
            new ApplicationUser { UserName = "unrelated", IsActive = true });
        await database.SaveChangesAsync();
        var users = scope.ServiceProvider.GetRequiredService<IUserService>();

        foreach (bool active in new[] { true, false })
        {
            var page = await users.SearchAsync(new UserParameterFilter
            {
                IsActive = active,
                Keyword = "match-",
                PageNumber = 2,
                PageSize = 1,
                OrderBy = new[] { "UserName" }
            }, default);

            Assert.Equal(2, page.TotalCount);
            Assert.Equal(2, page.TotalPages);
            Assert.Equal(active, Assert.Single(page.Data).IsActive);
        }

        var allStatuses = await users.SearchAsync(new UserParameterFilter { Keyword = "match-" }, default);
        Assert.Equal(4, allStatuses.TotalCount);
        Assert.Equal(4, allStatuses.Data.Count);
    }

    private sealed class AllTrailsSpecification : Specification<Trail>
    {
    }
}
