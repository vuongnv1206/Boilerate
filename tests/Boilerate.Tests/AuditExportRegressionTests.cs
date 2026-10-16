using Ardalis.Specification.EntityFrameworkCore;
using Boilerate.Application.Auditing;
using Boilerate.Domain.Auditing;
using Boilerate.Infrastructure.Persistence.Context;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Boilerate.Tests;

[Collection("Foundation")]
public sealed class AuditExportRegressionTests
{
    [Theory]
    [InlineData("not-a-guid")]
    [InlineData(" ")]
    public void InvalidUserIdsCannotSilentlyBroadenTheExport(string userId)
    {
        Assert.Throws<ValidationException>(() => new AuditLogsExportSpec(new() { UserId = userId }));
    }

    [Fact]
    public void ReversedDateRangesAreRejected()
    {
        var start = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        Assert.Throws<ValidationException>(() => new AuditLogsExportSpec(new() { StartDate = start, EndDate = start.AddDays(-1) }));
    }

    [Fact]
    public void AdvancedExportFiltersUseTheSearchFieldAllowlist()
    {
        Assert.Throws<ValidationException>(() => new AuditLogsExportSpec(new()
        {
            AdvancedSearch = new() { Fields = new[] { "OldValues" }, Keyword = "sensitive" }
        }));
        Assert.Throws<ValidationException>(() => new AuditLogsExportSpec(new()
        {
            AdvancedFilter = new() { Field = "NewValues", Operator = "contains", Value = "sensitive" }
        }));
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql")]
    [InlineData("postgresql")]
    public async Task ExportCombinesInheritedAndExplicitFiltersWithStableOrdering(string provider)
    {
        await using var fixture = new FoundationDatabase(provider);
        await fixture.CreateAsync();
        using var scope = fixture.Scope();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var actorId = Guid.NewGuid();
        var timestamp = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var first = new Trail { UserId = actorId, TableName = "SalesOrders", Type = TrailType.Create, DateTime = timestamp, PrimaryKey = "first", OldValues = "private-value" };
        var second = new Trail { UserId = actorId, TableName = "SalesOrders", Type = TrailType.Create, DateTime = timestamp, PrimaryKey = "second" };
        database.Entry(first).Property(trail => trail.Id).CurrentValue = Guid.Parse("00000000-0000-0000-0000-000000000001");
        database.Entry(second).Property(trail => trail.Id).CurrentValue = Guid.Parse("00000000-0000-0000-0000-000000000002");
        database.Trails.AddRange(first, second,
            new Trail { UserId = actorId, TableName = "SalesOrders", Type = TrailType.Delete, DateTime = timestamp, PrimaryKey = "deleted" },
            new Trail { UserId = actorId, TableName = "Inventory", Type = TrailType.Create, DateTime = timestamp, PrimaryKey = "stock" },
            new Trail { UserId = Guid.NewGuid(), TableName = "SalesOrders", Type = TrailType.Create, DateTime = timestamp, PrimaryKey = "another-actor" });
        await database.SaveChangesAsync();

        var specification = new AuditLogsExportSpec(new()
        {
            Keyword = "sales",
            AdvancedSearch = new() { Fields = new[] { "TableName" }, Keyword = "Orders" },
            AdvancedFilter = new() { Field = "Type", Operator = "eq", Value = "Create" },
            UserId = actorId.ToString(),
            TableName = "SalesOrders",
            StartDate = timestamp.AddMinutes(-1),
            EndDate = timestamp.AddMinutes(1)
        });
        var result = await database.Trails.AsNoTracking().WithSpecification(specification).ToListAsync();

        Assert.Collection(result,
            trail => Assert.Equal(second.Id, trail.Id),
            trail => Assert.Equal(first.Id, trail.Id));
        var sensitiveKeyword = new AuditLogsExportSpec(new() { Keyword = "private-value" });
        Assert.Empty(await database.Trails.AsNoTracking().WithSpecification(sensitiveKeyword).ToListAsync());
    }
}
