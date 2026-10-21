using System.Net;
using System.Net.Http.Json;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Identity.Roles;
using Boilerate.Application.Identity.Tokens;
using Boilerate.Application.Identity.Users;
using Boilerate.Domain.Identity;
using Boilerate.Infrastructure.Persistence.Context;
using Boilerate.Infrastructure.Persistence.Initialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Boilerate.Tests;

[Collection("Foundation")]
public sealed class GroupedAuthorizationTests
{
    private const string Password = "IsolatedAclTest!482";

    private static async Task<string> Admin(FoundationDatabase fixture)
    {
        using var scope = fixture.Scope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        Assert.True((await roles.CreateAsync(new ApplicationRole("Admin", "Administration"))).Succeeded);
        var user = new ApplicationUser { UserName = "acl-admin@example.test", Email = "acl-admin@example.test", IsActive = true, EmailConfirmed = true };
        Assert.True((await users.CreateAsync(user, Password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, "Admin")).Succeeded);
        await scope.ServiceProvider.GetRequiredService<FoundationBootstrap>().RunAsync(["--register-capabilities", "--grant-admin-capabilities"]);
        return user.Id;
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task UpgradeBackfillsGroupsWithoutChangingExistingGrants(string provider)
    {
        await using var fixture = new FoundationDatabase(provider);
        await fixture.CreateAsync(migrate: false);
        using var scope = fixture.Scope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var previous = db.Database.GetMigrations().Single(x => x.EndsWith("_RefundsReturns", StringComparison.Ordinal));
        await db.GetService<IMigrator>().MigrateAsync(previous);
        var functionId = Guid.NewGuid();
        // The old schema deliberately has no GroupId column yet.
        await db.Database.ExecuteSqlRawAsync("INSERT INTO \"Identity\".\"Functions\" (\"Id\",\"Code\",\"NormalizedCode\",\"Name\",\"IsActive\",\"Version\") VALUES ({0},{1},{2},{3},{4},{5})", functionId, "Product", "PRODUCT", "Original product", true, "original-version");
        var role = new ApplicationRole("Existing", "Existing grants");
        var action = new Boilerate.Domain.Identity.Action("View");
        db.Roles.Add(role); db.Actions.Add(action);
        db.ActionInFunctions.Add(new ActionInFunction(action.Id, functionId));
        var grant = new Permission(role.Id, functionId, action.Id); db.Permissions.Add(grant);
        await db.SaveChangesAsync();
        await db.Database.MigrateAsync();
        db.ChangeTracker.Clear();
        var function = await db.Functions.SingleAsync();
        Assert.Equal(functionId, function.Id); Assert.Equal(FunctionGroup.CatalogId, function.GroupId);
        Assert.Equal("original-version", function.Version); Assert.Equal("Original product", function.Name);
        Assert.Equal(grant.Id, (await db.Permissions.SingleAsync()).Id);
        Assert.Equal(4, await db.FunctionGroups.CountAsync());
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task GroupCrudAndRegistrationPreserveOperatorChoicesAndDatabaseConstraints(string provider)
    {
        await using var fixture = new FoundationDatabase(provider); await fixture.CreateAsync(); var admin = await Admin(fixture);
        using var scope = fixture.Scope(Guid.Parse(admin));
        var groups = scope.ServiceProvider.GetRequiredService<IFunctionGroupService>();
        var capabilities = scope.ServiceProvider.GetRequiredService<ICapabilityService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var group = await groups.CreateAsync(new() { Code = "Finance", Name = "Tài chính", SortOrder = 4 }, default);
        await Assert.ThrowsAsync<ConflictException>(() => groups.CreateAsync(new() { Code = "finance", Name = "Duplicate" }, default));
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => groups.CreateAsync(new() { Code = "Bad", Name = "Bad", SortOrder = -1 }, default));
        var renamed = await groups.UpdateAsync(group.Id, new() { Code = group.Code, Name = "Kế toán", SortOrder = 1, Version = group.Version }, default);
        await Assert.ThrowsAsync<ConflictException>(() => groups.UpdateAsync(group.Id, new() { Code = group.Code, Name = "Stale", Version = group.Version }, default));
        var function = (await capabilities.GetFunctionsAsync(default)).Single(f => f.Code == "User");
        await capabilities.UpdateFunctionAsync(function.Id, new() { Code = function.Code, Name = function.Name, IsActive = true, Version = function.Version, GroupId = group.Id }, default);
        var grantCount = await db.Permissions.CountAsync();
        await scope.ServiceProvider.GetRequiredService<FoundationBootstrap>().RunAsync(["--register-capabilities"]);
        Assert.Equal(group.Id, (await capabilities.GetFunctionAsync(function.Id, default)).GroupId);
        Assert.Equal(grantCount, await db.Permissions.CountAsync());
        await Assert.ThrowsAsync<ConflictException>(() => groups.DeleteAsync(group.Id, renamed.Version, default));
        var custom = await capabilities.CreateFunctionAsync(new() { Code = "Custom", Name = "Custom" }, default);
        Assert.Equal(FunctionGroup.OtherId, custom.GroupId);
        var empty = await groups.CreateAsync(new() { Code = "Empty", Name = "Empty" }, default);
        await groups.DeleteAsync(empty.Id, empty.Version, default);
        Assert.DoesNotContain(await groups.ListAsync(default), g => g.Id == empty.Id);
        db.Functions.Add(new Function { Code = "InvalidGroup", NormalizedCode = "INVALIDGROUP", Name = "Invalid", GroupId = Guid.NewGuid() });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task InactiveAssignmentsCanBeRetainedOrRemovedButNeverGrantedAnew(string provider)
    {
        await using var fixture = new FoundationDatabase(provider); await fixture.CreateAsync(); var admin = await Admin(fixture);
        using var scope = fixture.Scope(Guid.Parse(admin));
        var capabilities = scope.ServiceProvider.GetRequiredService<ICapabilityService>();
        var roles = scope.ServiceProvider.GetRequiredService<IRoleService>();
        var function = await capabilities.CreateFunctionAsync(new() { Code = "Invoices", Name = "Hóa đơn" }, default);
        var action = await capabilities.CreateActionAsync(new() { Code = "Approve", Name = "Duyệt" }, default);
        function = await capabilities.UpdateActionsAsync(function.Id, new() { Version = function.Version, ActiveActionIds = [action.Id] }, default);
        var roleId = await roles.CreateOrUpdateAsync(new() { Name = "Approver" });
        List<PermissionRequest> pairs = [new() { FunctionId = function.Id, ActionId = action.Id }];
        await roles.UpdatePermissionsAsync(new() { RoleId = roleId, Version = (await roles.GetByIdAsync(roleId)).Version, Permissions = pairs }, default);
        await capabilities.UpdateActionAsync(action.Id, new() { Code = action.Code, Name = action.Name, IsActive = false, Version = action.Version }, default);
        await roles.UpdatePermissionsAsync(new() { RoleId = roleId, Version = (await roles.GetByIdAsync(roleId)).Version, Permissions = pairs }, default);
        function = await capabilities.UpdateActionsAsync(function.Id, new() { Version = function.Version, ActiveActionIds = [action.Id] }, default);
        Assert.True(Assert.Single((await roles.GetByIdWithPermissionsAsync(roleId, default)).Single(f => f.Id == function.Id).ActionDtos).Selected);
        var otherId = await roles.CreateOrUpdateAsync(new() { Name = "OtherApprover" });
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => roles.UpdatePermissionsAsync(new() { RoleId = otherId, Version = roles.GetByIdAsync(otherId).GetAwaiter().GetResult().Version, Permissions = pairs }, default));
        await roles.UpdatePermissionsAsync(new() { RoleId = roleId, Version = (await roles.GetByIdAsync(roleId)).Version, Permissions = [] }, default);
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => roles.UpdatePermissionsAsync(new() { RoleId = roleId, Version = roles.GetByIdAsync(roleId).GetAwaiter().GetResult().Version, Permissions = pairs }, default));
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task HttpRoleGrantAssignmentAndRevocationUseCurrentDatabaseAndCentralAdminMembership(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, catalog: true); await fixture.CreateAsync(); var adminId = await Admin(fixture);
        string workerId;
        using (var scope = fixture.Scope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var worker = new ApplicationUser { UserName = "acl-worker@example.test", Email = "acl-worker@example.test", IsActive = true, EmailConfirmed = true };
            Assert.True((await users.CreateAsync(worker, Password)).Succeeded); workerId = worker.Id;
        }
        await using var host = await TestHostApplication.StartAsync(fixture, true);
        using var client = host.GetTestClient();
        var adminLogin = await client.PostAsJsonAsync("/api/tokens/get", new TokenRequest("acl-admin@example.test", Password)); adminLogin.EnsureSuccessStatusCode();
        var adminToken = (await adminLogin.Content.ReadFromJsonAsync<TokenResponse>())!;
        client.DefaultRequestHeaders.Authorization = new("Bearer", adminToken.Token);
        var context = (await client.GetFromJsonAsync<AuthorizationContextDto>("/api/personal/authorization"))!;
        Assert.True(context.IsCentralAdmin);
        Assert.Equal(await client.GetFromJsonAsync<List<string>>("/api/personal/permissions"), context.Permissions);
        var createRole = await client.PostAsJsonAsync("/api/roles", new { name = "BrowserOperator" }); createRole.EnsureSuccessStatusCode();
        var roleId = (await createRole.Content.ReadAsStringAsync()).Trim('"');
        var role = (await client.GetFromJsonAsync<RoleDto>($"/api/roles/{roleId}"))!;
        var functions = (await client.GetFromJsonAsync<List<FunctionDto>>($"/api/roles/{roleId}/permissions"))!;
        var category = functions.Single(f => f.Code == "Category"); var function = functions.Single(f => f.Code == "Function");
        var grants = new[] { new PermissionRequest { FunctionId = category.Id, ActionId = category.ActionDtos.Single(a => a.Code == "View").Id }, new PermissionRequest { FunctionId = function.Id, ActionId = function.ActionDtos.Single(a => a.Code == "View").Id } };
        (await client.PutAsJsonAsync($"/api/roles/{roleId}/permissions", new { version = role.Version, permissions = grants })).EnsureSuccessStatusCode();
        var workerDto = (await client.GetFromJsonAsync<UserDetailDto>($"/api/users/{workerId}"))!;
        (await client.PutAsJsonAsync($"/api/users/{workerId}/roles", new { version = workerDto.Version, roleIds = new[] { roleId } })).EnsureSuccessStatusCode();
        var workerLogin = await client.PostAsJsonAsync("/api/tokens/get", new TokenRequest("acl-worker@example.test", Password)); workerLogin.EnsureSuccessStatusCode();
        var token = (await workerLogin.Content.ReadFromJsonAsync<TokenResponse>())!;
        using var employee = host.GetTestClient(); employee.DefaultRequestHeaders.Authorization = new("Bearer", token.Token);
        var employeeContext = (await employee.GetFromJsonAsync<AuthorizationContextDto>("/api/personal/authorization"))!;
        Assert.False(employeeContext.IsCentralAdmin); Assert.Contains("Permissions.Function.View", employeeContext.Permissions);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.GetAsync("/api/functions")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.GetAsync("/api/function-groups")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await employee.GetAsync("/api/categories")).StatusCode);
        role = (await client.GetFromJsonAsync<RoleDto>($"/api/roles/{roleId}"))!;
        (await client.PutAsJsonAsync($"/api/roles/{roleId}/permissions", new { version = role.Version, permissions = Array.Empty<PermissionRequest>() })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.GetAsync("/api/categories")).StatusCode);
        Assert.Empty((await employee.GetFromJsonAsync<AuthorizationContextDto>("/api/personal/authorization"))!.Permissions);
        Assert.NotEqual(adminId, workerId);
    }
}
