using Boilerate.Domain.Identity;
using Boilerate.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Boilerate.Tests;

[Collection("Foundation")]
public sealed class FreshBaselineMigrationTests
{
    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task FreshDatabaseUsesOneBaselineWithoutLegacyTokenOrReadState(string provider)
    {
        await using var fixture = new FoundationDatabase(provider);
        await fixture.CreateAsync(migrate: false);
        using var scope = fixture.Scope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await db.Database.MigrateAsync();

        var migration = Assert.Single(await db.Database.GetAppliedMigrationsAsync());
        Assert.EndsWith("_InitialFoundation", migration, StringComparison.Ordinal);
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Empty(await db.Users.ToListAsync());
        Assert.Empty(await db.Roles.ToListAsync());
        Assert.Empty(await db.Actions.ToListAsync());
        Assert.Empty(await db.Functions.ToListAsync());
        Assert.Empty(await db.Permissions.ToListAsync());
        Assert.Equal(1, await db.AuthorizationLocks.CountAsync());

        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT table_schema, table_name, column_name FROM information_schema.columns";
        await using var reader = await command.ExecuteReaderAsync();
        var columns = new HashSet<string>(StringComparer.Ordinal);
        while (await reader.ReadAsync())
        {
            columns.Add($"{reader.GetString(0)}.{reader.GetString(1)}.{reader.GetString(2)}");
        }

        Assert.DoesNotContain("Identity.Users.RefreshToken", columns);
        Assert.DoesNotContain("Identity.Users.RefreshTokenExpiryTime", columns);
        Assert.DoesNotContain("Notifications.Notifications.IsRead", columns);
        Assert.DoesNotContain("Notifications.Notifications.ReadOn", columns);
        Assert.Contains("Identity.UserSessions.RefreshTokenHash", columns);
        Assert.Contains("Notifications.NotificationReceipts.IsRead", columns);
        Assert.Contains("Notifications.NotificationReceipts.ReadOn", columns);
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task BaselineEnforcesValidGrantPairsAndUniquenessAcrossSoftDelete(string provider)
    {
        await using var fixture = new FoundationDatabase(provider);
        await fixture.CreateAsync();
        var action = new Domain.Identity.Action("View");
        var function = new Function { Code = "Orders", NormalizedCode = "ORDERS", Name = "Orders" };
        var role = new ApplicationRole("Sales");
        using (var scope = fixture.Scope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Actions.Add(action);
            db.Functions.Add(function);
            db.Roles.Add(role);
            await db.SaveChangesAsync();
        }

        // Existing action/function rows are insufficient: the supported pair is a separate constraint.
        using (var scope = fixture.Scope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Permissions.Add(new Permission(role.Id, function.Id, action.Id));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        Guid grantId;
        using (var scope = fixture.Scope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.ActionInFunctions.Add(new ActionInFunction(action.Id, function.Id));
            var grant = new Permission(role.Id, function.Id, action.Id);
            db.Permissions.Add(grant);
            await db.SaveChangesAsync();
            grantId = grant.Id;
            db.Permissions.Remove(grant);
            await db.SaveChangesAsync();
            Assert.Empty(await db.Permissions.ToListAsync());
        }

        // A revoked grant must be restored by the service, not duplicated with a new ID.
        using (var scope = fixture.Scope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Permissions.Add(new Permission(role.Id, function.Id, action.Id));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        using (var scope = fixture.Scope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var grant = await db.Permissions.IgnoreQueryFilters().AsNoTracking().SingleAsync();
            Assert.Equal(grantId, grant.Id);
            Assert.NotNull(grant.DeletedOn);
            db.ActionInFunctions.Remove(await db.ActionInFunctions.SingleAsync());
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
    }
}
