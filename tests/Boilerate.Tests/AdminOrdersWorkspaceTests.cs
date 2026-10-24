using System.Net;
using System.Net.Http.Json;
using Boilerate.Application.Catalog;
using Boilerate.Application.Identity.Tokens;
using Boilerate.Application.Ordering;
using Boilerate.Domain.Identity;
using Boilerate.Domain.Ordering;
using Boilerate.Infrastructure.Persistence.Context;
using Boilerate.Infrastructure.Persistence.Initialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Boilerate.Tests;

[Collection("Foundation")]
public sealed class AdminOrdersWorkspaceTests
{
    private const string Email = "admin-workspace@example.test";
    private const string Password = "AdminWorkspacePassword!29874";

    private static async Task SeedAsync(FoundationDatabase fixture)
    {
        fixture.Services.GetRequiredService<IHostEnvironment>().EnvironmentName = Environments.Development;
        using var scope = fixture.Scope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        Assert.True((await roles.CreateAsync(new ApplicationRole("Admin", "Acceptance administrator"))).Succeeded);
        var user = new ApplicationUser { UserName = Email, Email = Email, IsActive = true, EmailConfirmed = true };
        Assert.True((await users.CreateAsync(user, Password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, "Admin")).Succeeded);
        await scope.ServiceProvider.GetRequiredService<FoundationBootstrap>().RunAsync(["--register-capabilities", "--grant-admin-capabilities"]);
        await scope.ServiceProvider.GetRequiredService<CustomSeederRunner>().RunAsync();
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task HttpFiltersStablePagingSafeHistoryAndVersionConflictUseTheSeededWorkspace(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true, configOverrides: new Dictionary<string, string?>
        {
            ["DevelopmentSeed:ActorEmail"] = Email,
            ["Commerce:WorkerEnabled"] = "false"
        });
        await fixture.CreateAsync();
        await SeedAsync(fixture);
        await using var app = await TestHostApplication.StartAsync(fixture, true);
        using var client = app.GetTestClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/orders")).StatusCode);
        var login = await client.PostAsJsonAsync("/api/tokens/get", new TokenRequest(Email, Password));
        login.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<TokenResponse>())!.Token);

        var all = (await client.GetFromJsonAsync<Page<OrderSummary>>("/api/admin/orders?Size=100"))!;
        Assert.Equal(8, all.Total);
        Assert.Equal(all.Items.OrderByDescending(order => order.CreatedOn).ThenByDescending(order => order.Id).Select(order => order.Id),
            all.Items.Select(order => order.Id));
        var first = (await client.GetFromJsonAsync<Page<OrderSummary>>("/api/admin/orders?Size=3&Page=1"))!;
        var next = (await client.GetFromJsonAsync<Page<OrderSummary>>("/api/admin/orders?Size=3&Page=2"))!;
        Assert.Equal(8, first.Total);
        Assert.Equal(8, next.Total);
        Assert.Equal(all.Items.Take(3).Select(order => order.Id), first.Items.Select(order => order.Id));
        Assert.Equal(all.Items.Skip(3).Take(3).Select(order => order.Id), next.Items.Select(order => order.Id));
        Assert.Empty(first.Items.Select(order => order.Id).Intersect(next.Items.Select(order => order.Id)));
        Assert.All(all.Items, order => Assert.Equal(DateTimeKind.Utc, order.CreatedOn.Kind));
        var searched = (await client.GetFromJsonAsync<Page<OrderSummary>>($"/api/admin/orders?Search={Uri.EscapeDataString(all.Items[0].Number.ToLowerInvariant())}"))!;
        Assert.Equal(all.Items[0].Id, Assert.Single(searched.Items).Id);
        Assert.Equal(1, searched.Total);
        Assert.Equal(8, (await client.GetFromJsonAsync<Page<OrderSummary>>("/api/admin/orders?Search=ORDERS-DEVELOPMENT%40EXAMPLE.TEST"))!.Total);
        Assert.Equal(8, (await client.GetFromJsonAsync<Page<OrderSummary>>("/api/admin/orders?Search=0900000000"))!.Total);
        Assert.Equal(1, (await client.GetFromJsonAsync<Page<OrderSummary>>("/api/admin/orders?Status=Cancelled"))!.Total);
        Assert.Equal(3, (await client.GetFromJsonAsync<Page<OrderSummary>>("/api/admin/orders?PaymentStatus=Paid"))!.Total);
        Assert.Equal(1, (await client.GetFromJsonAsync<Page<OrderSummary>>("/api/admin/orders?PaymentStatus=Paid&FulfillmentStatus=NeedsReview"))!.Total);
        Assert.Equal(8, (await client.GetFromJsonAsync<Page<OrderSummary>>("/api/admin/orders?CreatedFrom=2000-01-01T00%3A00%3A00Z&CreatedTo=2100-01-01T00%3A00%3A00Z"))!.Total);
        Assert.Empty((await client.GetFromJsonAsync<Page<OrderSummary>>("/api/admin/orders?CreatedFrom=2100-01-01T00%3A00%3A00Z"))!.Items);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/admin/orders?CreatedFrom=2027-01-01&CreatedTo=2026-01-01")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/admin/orders?Size=0")).StatusCode);

        var completed = all.Items.First(order => order.Status == OrderStatus.Completed);
        var historyResponse = await client.GetAsync($"/api/admin/orders/{completed.Id}/history");
        historyResponse.EnsureSuccessStatusCode();
        Assert.True(historyResponse.Headers.CacheControl?.NoStore);
        var historyJson = await historyResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("oldValues", historyJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("newValues", historyJson, StringComparison.OrdinalIgnoreCase);
        var history = (await historyResponse.Content.ReadFromJsonAsync<List<OrderHistoryEntryDto>>())!;
        Assert.Contains(history, entry => entry.RelatedEntityType == "Orders");
        Assert.Contains(history, entry => entry.RelatedEntityType == "Shipments");
        Assert.Contains(history, entry => entry.RelatedEntityType == "PaymentReceipts");
        Assert.All(history, entry => Assert.Equal(DateTimeKind.Utc, entry.OccurredAtUtc.Kind));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/admin/orders/{Guid.NewGuid()}/history")).StatusCode);

        var cancellable = all.Items.First(order => order.Status == OrderStatus.AwaitingProcessing);
        var detail = (await client.GetFromJsonAsync<OrderDto>($"/api/admin/orders/{cancellable.Id}"))!;
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/admin/orders/{detail.Id}/cancel", new CancelOrder("stale-version"))).StatusCode);
        Assert.Equal(detail.Version, (await client.GetFromJsonAsync<OrderDto>($"/api/admin/orders/{detail.Id}"))!.Version);
        var cancelledResponse = await client.PostAsJsonAsync($"/api/admin/orders/{detail.Id}/cancel", new CancelOrder(detail.Version));
        cancelledResponse.EnsureSuccessStatusCode();
        var cancelled = (await cancelledResponse.Content.ReadFromJsonAsync<OrderDto>())!;
        Assert.Equal(OrderStatus.Cancelled, cancelled.Status);
        Assert.Equal(detail.Items, cancelled.Items);
        Assert.Equal(detail.Total, cancelled.Total);

        using (var scope = fixture.Scope())
            await scope.ServiceProvider.GetRequiredService<CustomSeederRunner>().RunAsync();
        Assert.Equal(8, (await client.GetFromJsonAsync<Page<OrderSummary>>("/api/admin/orders"))!.Total);
        Assert.Equal(cancelled.Version, (await client.GetFromJsonAsync<OrderDto>($"/api/admin/orders/{cancelled.Id}"))!.Version);
        using (var scope = fixture.Scope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var functionId = await db.Functions.Where(function => function.Code == "Order").Select(function => function.Id).SingleAsync();
            var actionId = await db.Actions.Where(action => action.Code == "View").Select(action => action.Id).SingleAsync();
            await db.Permissions.Where(grant => grant.FunctionId == functionId && grant.ActionId == actionId)
                .ExecuteUpdateAsync(set => set.SetProperty(grant => grant.DeletedOn, DateTime.UtcNow));
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/orders")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/admin/orders/{detail.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/admin/orders/{detail.Id}/history")).StatusCode);
    }
}
