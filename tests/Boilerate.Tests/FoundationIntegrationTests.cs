using System.Net;
using System.Net.Http.Json;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Identity.Roles;
using Boilerate.Application.Identity.Tokens;
using Boilerate.Application.Identity.Users;
using Boilerate.Application.Notifications;
using Boilerate.Domain.Identity;
using Boilerate.Domain.Notifications;
using Boilerate.Host.Configurations;
using Boilerate.Host.Controllers.Identity;
using Boilerate.Infrastructure;
using Boilerate.Infrastructure.Persistence.Context;
using Boilerate.Infrastructure.Persistence.Initialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Boilerate.Tests;

[Collection("Foundation")]
public sealed class FoundationIntegrationTests
{
    private const string Password = "IsolatedTestPassword!482";
    private static async Task<Guid> Bootstrap(FoundationDatabase fixture)
    {
        var email = Environment.GetEnvironmentVariable("BOILERATE_BOOTSTRAP_EMAIL");
        var password = Environment.GetEnvironmentVariable("BOILERATE_BOOTSTRAP_PASSWORD");
        try
        {
            Environment.SetEnvironmentVariable("BOILERATE_BOOTSTRAP_EMAIL", "admin@example.test");
            Environment.SetEnvironmentVariable("BOILERATE_BOOTSTRAP_PASSWORD", Password);
            using var scope = fixture.Scope();
            await scope.ServiceProvider.GetRequiredService<FoundationBootstrap>().RunAsync(new[] { "--bootstrap-admin" });
            return Guid.Parse((await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.SingleAsync()).Id);
        }
        finally
        {
            Environment.SetEnvironmentVariable("BOILERATE_BOOTSTRAP_EMAIL", email);
            Environment.SetEnvironmentVariable("BOILERATE_BOOTSTRAP_PASSWORD", password);
        }
    }
    private static async Task<ApplicationUser> User(FoundationDatabase fixture, string name)
    {
        using var scope = fixture.Scope();
        var user = new ApplicationUser { UserName = name, Email = name + "@example.test", EmailConfirmed = true, IsActive = true };
        Assert.True((await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(user, Password)).Succeeded);
        return user;
    }
    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task GrantsUseCurrentDatabaseStateAndRestoreSoftDeletedRows(string provider)
    {
        await using var fixture = new FoundationDatabase(provider); await fixture.CreateAsync();
        var admin = await Bootstrap(fixture);
        var user = await User(fixture, "worker");
        Guid functionId, actionId; string roleId, secondRoleId;
        using (var scope = fixture.Scope(admin))
        {
            var capabilities = scope.ServiceProvider.GetRequiredService<ICapabilityService>();
            var function = await capabilities.CreateFunctionAsync(new() { Code = "Invoices", Name = "Hóa đơn" }, default);
            var action = await capabilities.CreateActionAsync(new() { Code = "Approve", Name = "Duyệt" }, default);
            function = await capabilities.UpdateActionsAsync(function.Id, new() { Version = function.Version, ActiveActionIds = new() { action.Id } }, default);
            functionId = function.Id; actionId = action.Id;
            var roles = scope.ServiceProvider.GetRequiredService<IRoleService>();
            roleId = await roles.CreateOrUpdateAsync(new() { Name = "Approver" });
            secondRoleId = await roles.CreateOrUpdateAsync(new() { Name = "SecondApprover" });
            foreach (var id in new[] { roleId, secondRoleId })
                await roles.UpdatePermissionsAsync(new() { RoleId = id, Version = (await roles.GetByIdAsync(id)).Version,
                    Permissions = new() { new() { FunctionId = functionId, ActionId = actionId } } }, default);
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var worker = await db.Users.SingleAsync(x => x.Id == user.Id);
            await scope.ServiceProvider.GetRequiredService<IUserService>().AssignRolesAsync(user.Id,
                new() { Version = worker.ConcurrencyStamp!, RoleIds = new() { roleId, secondRoleId } }, default);
        }
        using (var scope = fixture.Scope(Guid.Parse(user.Id)))
            Assert.True(await scope.ServiceProvider.GetRequiredService<IUserService>().HasPermissionAsync(user.Id, "Permissions.Invoices.Approve"));
        using (var scope = fixture.Scope(admin))
        {
            var roles = scope.ServiceProvider.GetRequiredService<IRoleService>();
            await roles.UpdatePermissionsAsync(new() { RoleId = roleId, Version = (await roles.GetByIdAsync(roleId)).Version, Permissions = new() }, default);
        }
        using (var scope = fixture.Scope(Guid.Parse(user.Id)))
            Assert.True(await scope.ServiceProvider.GetRequiredService<IUserService>().HasPermissionAsync(user.Id, "Permissions.Invoices.Approve"));
        using (var scope = fixture.Scope(admin))
        {
            var roles = scope.ServiceProvider.GetRequiredService<IRoleService>();
            await roles.UpdatePermissionsAsync(new() { RoleId = secondRoleId, Version = (await roles.GetByIdAsync(secondRoleId)).Version, Permissions = new() }, default);
        }
        using (var scope = fixture.Scope(Guid.Parse(user.Id)))
            Assert.False(await scope.ServiceProvider.GetRequiredService<IUserService>().HasPermissionAsync(user.Id, "Permissions.Invoices.Approve"));
        using (var scope = fixture.Scope(admin))
        {
            var roles = scope.ServiceProvider.GetRequiredService<IRoleService>();
            var version = (await roles.GetByIdAsync(roleId)).Version;
            await roles.UpdatePermissionsAsync(new() { RoleId = roleId, Version = version,
                Permissions = new() { new() { FunctionId = functionId, ActionId = actionId } } }, default);
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(1, await db.Permissions.IgnoreQueryFilters().CountAsync(x => x.RoleId == roleId && x.FunctionId == functionId && x.ActionId == actionId));
            Assert.True(await db.Trails.AnyAsync(x => x.TableName == "RolePermissions"));
            var capability = await scope.ServiceProvider.GetRequiredService<ICapabilityService>().GetFunctionAsync(functionId, default);
            await scope.ServiceProvider.GetRequiredService<ICapabilityService>().UpdateFunctionAsync(functionId,
                new() { Code = capability.Code, Name = "Nhãn mới", Version = capability.Version }, default);
            await Assert.ThrowsAsync<ConflictException>(() => roles.UpdatePermissionsAsync(new()
                { RoleId = roleId, Version = version, Permissions = new() }, default));
        }
        using (var scope = fixture.Scope(admin))
        {
            var roles = scope.ServiceProvider.GetRequiredService<IRoleService>();
            await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => roles.UpdatePermissionsAsync(new()
                { RoleId = roleId, Version = (roles.GetByIdAsync(roleId).GetAwaiter().GetResult()).Version,
                  Permissions = new() { new() { FunctionId = functionId, ActionId = Guid.NewGuid() } } }, default));
        }
        using (var scope = fixture.Scope(Guid.Parse(user.Id)))
            Assert.True(await scope.ServiceProvider.GetRequiredService<IUserService>().HasPermissionAsync(user.Id, "Permissions.Invoices.Approve"));
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task SessionsAreIndependentAndReusedRefreshRevokesOnlyItsFamily(string provider)
    {
        await using var fixture = new FoundationDatabase(provider); await fixture.CreateAsync(); await Bootstrap(fixture);
        TokenResponse first, second, rotated;
        using (var scope = fixture.Scope())
        {
            var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
            first = await tokens.GetTokenAsync(new TokenRequest("admin@example.test", Password), "127.0.0.1", default);
            second = await tokens.GetTokenAsync(new TokenRequest("admin@example.test", Password), "127.0.0.2", default);
            Assert.NotEqual(first.SessionId, second.SessionId);
            Assert.DoesNotContain(first.RefreshToken, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().UserSessions.Select(x => x.RefreshTokenHash).ToListAsync());
        }
        using (var scope = fixture.Scope())
            rotated = await scope.ServiceProvider.GetRequiredService<ITokenService>().RefreshTokenAsync(new() { RefreshToken = first.RefreshToken }, "127.0.0.1");
        using (var scope = fixture.Scope())
            await Assert.ThrowsAsync<UnauthorizedException>(() => scope.ServiceProvider.GetRequiredService<ITokenService>()
                .RefreshTokenAsync(new() { RefreshToken = first.RefreshToken }, "127.0.0.1"));
        using (var scope = fixture.Scope())
        {
            var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
            await Assert.ThrowsAsync<UnauthorizedException>(() => tokens.RefreshTokenAsync(new() { RefreshToken = rotated.RefreshToken }, "127.0.0.1"));
            // Reused-token rejection committed the family revocation.
        }
        using (var scope = fixture.Scope())
            Assert.NotNull(await scope.ServiceProvider.GetRequiredService<ITokenService>().RefreshTokenAsync(new() { RefreshToken = second.RefreshToken }, "127.0.0.2"));
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task NotificationReceiptsEnforceOwnershipAndIndependentReadState(string provider)
    {
        await using var fixture = new FoundationDatabase(provider); await fixture.CreateAsync(); var admin = await Bootstrap(fixture);
        var a = await User(fixture, "readerA"); var b = await User(fixture, "readerB");
        Guid broadcast, privateId, adminOnly;
        using (var scope = fixture.Scope(admin))
        {
            var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
            broadcast = await notifications.SendBroadcastAsync("Xin chào", "Thông báo chung", NotificationType.Info);
            privateId = await notifications.SendToUserAsync(Guid.Parse(a.Id), "Riêng", "Chỉ A", NotificationType.Info);
            adminOnly = await notifications.SendToRoleAsync("Admin", "Quản trị", "Riêng Admin", NotificationType.Info);
        }
        using (var scope = fixture.Scope(Guid.Parse(a.Id)))
        {
            var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
            await notifications.MarkAsReadAsync(broadcast);
            await notifications.DeleteAsync(privateId);
            Assert.DoesNotContain((await notifications.GetUserNotificationsAsync(Guid.Parse(a.Id), 1, 20)).Data, n => n.Id == adminOnly || n.Id == privateId);
        }
        using (var scope = fixture.Scope(Guid.Parse(b.Id)))
        {
            var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
            Assert.False((await notifications.GetUserNotificationsAsync(Guid.Parse(b.Id), 1, 20)).Data.Single(n => n.Id == broadcast).IsRead);
            await Assert.ThrowsAsync<NotFoundException>(() => notifications.DeleteAsync(privateId));
            await Assert.ThrowsAsync<NotFoundException>(() => notifications.MarkAsReadAsync(adminOnly));
        }
        using (var scope = fixture.Scope()) Assert.Equal(3, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Notifications.CountAsync());
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task HttpAuthenticationCatalogDiscoveryAndLastAdminProtection(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, catalog: true); await fixture.CreateAsync(); var admin = await Bootstrap(fixture);
        await using var server = await TestHostApplication.StartAsync(fixture, false); using var client = server.GetTestClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/notifications")).StatusCode);
        var login = await client.PostAsJsonAsync("/api/tokens/get", new TokenRequest("admin@example.test", Password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<TokenResponse>())!;
        client.DefaultRequestHeaders.Authorization = new("Bearer", token.Token);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/actions")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/products/" + Guid.NewGuid())).StatusCode);
        var basic = await User(fixture, "basic");
        using (var scope = fixture.Scope())
        {
            var basicToken = await scope.ServiceProvider.GetRequiredService<ITokenService>().GetTokenAsync(new TokenRequest(basic.Email!, Password), "127.0.0.2", default);
            using var basicClient = server.GetTestClient(); basicClient.DefaultRequestHeaders.Authorization = new("Bearer", basicToken.Token);
            Assert.Equal(HttpStatusCode.Forbidden, (await basicClient.GetAsync("/api/actions")).StatusCode);
        }
        var invalidCreate = await client.PostAsJsonAsync("/api/users/create", new CreateUserRequest
        {
            UserName = "invaliduser", Email = "invalid@example.test", FirstName = "Test", LastName = "User",
            Password = Password, ConfirmPassword = "different-password"
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidCreate.StatusCode);
        Assert.Contains("traceId", await invalidCreate.Content.ReadAsStringAsync());
        Assert.DoesNotContain("StackTrace", await invalidCreate.Content.ReadAsStringAsync());
        using (var scope = fixture.Scope()) Assert.False(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.AnyAsync(u => u.Email == "invalid@example.test"));
        Assert.DoesNotContain(server.Services.GetRequiredService<Microsoft.AspNetCore.Mvc.Infrastructure.IActionDescriptorCollectionProvider>().ActionDescriptors.Items,
            d => d is Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor c && c.ControllerName == "Products");
        using (var scope = fixture.Scope(admin))
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await Assert.ThrowsAsync<ConflictException>(() => scope.ServiceProvider.GetRequiredService<IUserService>().ToggleStatusAsync(
                new() { UserId = admin.ToString(), ActivateUser = false, Version = db.Users.Single(x => x.Id == admin.ToString()).ConcurrencyStamp! }, default));
        }
        await using var enabled = await TestHostApplication.StartAsync(fixture, true); using var enabledClient = enabled.GetTestClient();
        enabledClient.DefaultRequestHeaders.Authorization = new("Bearer", token.Token);
        Assert.Contains(enabled.Services.GetRequiredService<Microsoft.AspNetCore.Mvc.Infrastructure.IActionDescriptorCollectionProvider>().ActionDescriptors.Items,
            d => d is Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor c && c.ControllerName == "Products");
        Assert.Equal(HttpStatusCode.NotFound, (await enabledClient.GetAsync("/api/products/" + Guid.NewGuid())).StatusCode); // endpoint exists; missing product
        await client.DeleteAsync("/api/tokens/sessions/" + token.SessionId);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/actions")).StatusCode);
    }
    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task ConcurrentAdministrativeChangesCannotDisableEveryAdministrator(string provider)
    {
        await using var fixture = new FoundationDatabase(provider); await fixture.CreateAsync(); var a = await Bootstrap(fixture);
        var second = await User(fixture, "secondAdmin"); var b = Guid.Parse(second.Id);
        using (var scope = fixture.Scope(a))
            {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True((await manager.AddToRoleAsync((await manager.FindByIdAsync(second.Id))!, "Admin")).Succeeded);
        }
        string firstVersion, secondVersion;
        using (var scope = fixture.Scope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            firstVersion = (await db.Users.SingleAsync(u => u.Id == a.ToString())).ConcurrencyStamp!;
            secondVersion = (await db.Users.SingleAsync(u => u.Id == b.ToString())).ConcurrencyStamp!;
        }
        async Task<Exception?> Disable(Guid actor, string version)
        {
            using var scope = fixture.Scope(actor);
            try
            {
                await scope.ServiceProvider.GetRequiredService<IUserService>().ToggleStatusAsync(
                    new() { UserId = actor.ToString(), ActivateUser = false, Version = version }, default);
                return null;
            }
            catch (CustomException ex) { return ex; }
        }
        var outcomes = await Task.WhenAll(Disable(a, firstVersion), Disable(b, secondVersion));
        Assert.Single(outcomes.Where(x => x is null)); Assert.Single(outcomes.Where(x => x is not null));
        using (var scope = fixture.Scope()) Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.CountAsync(u => u.IsActive));
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task ConcurrentRefreshUsesOnceAndReuseRevokesTheFamily(string provider)
    {
        await using var fixture = new FoundationDatabase(provider); await fixture.CreateAsync(); await Bootstrap(fixture);
        TokenResponse original;
        using (var scope = fixture.Scope()) original = await scope.ServiceProvider.GetRequiredService<ITokenService>()
            .GetTokenAsync(new TokenRequest("admin@example.test", Password), "127.0.0.1", default);
        async Task<bool> Refresh()
        {
            using var scope = fixture.Scope();
            try { await scope.ServiceProvider.GetRequiredService<ITokenService>().RefreshTokenAsync(new() { RefreshToken = original.RefreshToken }, "127.0.0.1"); return true; }
            catch (UnauthorizedException) { return false; }
        }
        var results = await Task.WhenAll(Refresh(), Refresh()); Assert.Single(results.Where(x => x));
        using (var scope = fixture.Scope()) Assert.False(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().UserSessions.AnyAsync(x => x.RevokedOn == null));
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task ConcurrentRoleUpdatesRejectAStaleVersion(string provider)
    {
        await using var fixture = new FoundationDatabase(provider); await fixture.CreateAsync(); var admin = await Bootstrap(fixture);
        string id, version;
        using (var scope = fixture.Scope(admin))
        {
            var roles = scope.ServiceProvider.GetRequiredService<IRoleService>();
            id = await roles.CreateOrUpdateAsync(new() { Name = "Editor" }); version = (await roles.GetByIdAsync(id)).Version;
        }
        async Task<bool> Update(string name)
        {
            using var scope = fixture.Scope(admin);
            try { await scope.ServiceProvider.GetRequiredService<IRoleService>().CreateOrUpdateAsync(new() { Id = id, Version = version, Name = name }); return true; }
            catch (ConflictException) { return false; }
        }
        var outcomes = await Task.WhenAll(Update("FirstEditor"), Update("SecondEditor"));
        Assert.Single(outcomes.Where(x => x)); Assert.Single(outcomes.Where(x => !x));
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task ExplicitRegistrationDoesNotGrantAndCliGrantInvalidatesStaleRoleVersions(string provider)
    {
        await using var fixture = new FoundationDatabase(provider); await fixture.CreateAsync(); await Bootstrap(fixture);
        string roleId, initialVersion; int count;
        using (var scope = fixture.Scope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var role = await db.Roles.SingleAsync(r => r.NormalizedName == "ADMIN");
            roleId = role.Id; initialVersion = role.ConcurrencyStamp!; count = await db.Permissions.CountAsync();
        }
        fixture.Configuration["Modules:Catalog:Enabled"] = "true";
        using (var scope = fixture.Scope())
        {
            await scope.ServiceProvider.GetRequiredService<FoundationBootstrap>().RunAsync(new[] { "--register-capabilities" });
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(count, await db.Permissions.CountAsync());
            Assert.Equal(initialVersion, (await db.Roles.SingleAsync(r => r.Id == roleId)).ConcurrencyStamp);
            Assert.True(await db.Functions.AnyAsync(f => f.Code == "Product"));
        }
        string grantedVersion; int grantedCount;
        using (var scope = fixture.Scope())
        {
            await scope.ServiceProvider.GetRequiredService<FoundationBootstrap>().RunAsync(new[] { "--grant-admin-capabilities" });
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            grantedVersion = (await db.Roles.SingleAsync(r => r.Id == roleId)).ConcurrencyStamp!;
            grantedCount = await db.Permissions.CountAsync();
            Assert.NotEqual(initialVersion, grantedVersion); Assert.True(grantedCount > count);
            Assert.True(await db.Trails.AnyAsync(t => t.TableName == "Roles" && t.PrimaryKey == roleId && t.NewValues!.Contains("OperatorGrantAdmin")));
        }
        using (var scope = fixture.Scope())
        {
            await scope.ServiceProvider.GetRequiredService<FoundationBootstrap>().RunAsync(new[] { "--grant-admin-capabilities" });
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(grantedVersion, (await db.Roles.SingleAsync(r => r.Id == roleId)).ConcurrencyStamp);
            Assert.Equal(grantedCount, await db.Permissions.CountAsync());
        }
    }

}

[CollectionDefinition("Foundation", DisableParallelization = true)]
public sealed class FoundationCollection { }
