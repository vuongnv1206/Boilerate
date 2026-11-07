using System.Net;
using System.Net.Http.Json;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Identity.Tokens;
using Boilerate.Application.Ordering.Shared;
using Boilerate.Application.Overview;
using Boilerate.Application.Renewals;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.DigitalInventory;
using Boilerate.Domain.Identity;
using Boilerate.Domain.Inventory;
using Boilerate.Domain.Ordering;
using Boilerate.Domain.Payments;
using Boilerate.Domain.Renewals;
using Boilerate.Infrastructure.Persistence.Context;
using Boilerate.Infrastructure.Persistence.Initialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Boilerate.Tests;

[Collection("Foundation")]
public sealed class AdminOverviewTests
{
    private const string Password = "OverviewIsolatedPassword!984";
    private const string Hash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    private static FoundationDatabase Database(string provider) => new(provider, true,
        configOverrides: new Dictionary<string, string?> { ["Commerce:WorkerEnabled"] = "false" });

    private static async Task<Guid> AdminAsync(FoundationDatabase fixture)
    {
        using var scope = fixture.Scope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        Assert.True((await roles.CreateAsync(new ApplicationRole("Admin", "Overview test"))).Succeeded);
        var user = new ApplicationUser { UserName = "overview@example.test", Email = "overview@example.test", EmailConfirmed = true, IsActive = true };
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.True((await users.CreateAsync(user, Password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, "Admin")).Succeeded);
        await scope.ServiceProvider.GetRequiredService<FoundationBootstrap>().RunAsync(["--register-capabilities", "--grant-admin-capabilities"]);
        return Guid.Parse(user.Id);
    }

    private static async Task LoginAsync(HttpClient client, string email = "overview@example.test")
    {
        var response = await client.PostAsJsonAsync("/api/tokens/get", new TokenRequest(email, Password));
        response.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await response.Content.ReadFromJsonAsync<TokenResponse>())!.Token);
    }

    private static Product Product(ApplicationDbContext db, ProductType type, string key,
        ProductFulfillmentKind kind = ProductFulfillmentKind.Physical, bool published = true)
    {
        var product = new Product(type.Id, "Product " + key, "overview-" + key, fulfillmentKind: kind);
        product.AddVariant("OV-" + key, 100, null, new Dictionary<Guid, Guid>(), 100);
        if (published) product.ChangeStatus(ProductStatus.Published, type);
        db.Products.Add(product);
        return product;
    }

    private static Order Order(ApplicationDbContext db, Cart cart, Product product, DateTime now,
        OrderPaymentMethod method = OrderPaymentMethod.Prepaid, decimal price = 100)
    {
        var variant = product.Variants.Single();
        var item = new OrderItem(product.Id, variant.Id, product.Name, variant.Sku, "", product.FulfillmentKind, price, 1);
        var order = new Order(cart.CustomerId, cart.Id, Guid.NewGuid(), Hash, method, "Private customer",
            "customer-private@example.test", "0901234567", "Private address", 0, now, 30, [item]);
        db.Orders.Add(order);
        return order;
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task FinanceUsesVietnamBusinessDatesExactIntegerStringsAndIndependentRefundPeriod(string provider)
    {
        await using var fixture = Database(provider);
        await fixture.CreateAsync();
        var actor = await AdminAsync(fixture);
        using (var scope = fixture.Scope(actor))
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var type = new ProductType("OVFINANCE", "Overview finance");
            db.ProductTypes.Add(type);
            var product = Product(db, type, "finance");
            var cart = new Cart(actor);
            db.Carts.Add(cart);
            var now = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
            var beforeMidnight = new DateTime(2026, 1, 1, 16, 59, 59, DateTimeKind.Utc);
            var midnight = beforeMidnight.AddSeconds(1);
            var afterRange = new DateTime(2026, 1, 3, 17, 0, 0, DateTimeKind.Utc);
            const decimal large = 9007199254740993m;
            var first = Order(db, cart, product, now, price: large);
            first.RecordPayment(beforeMidnight);
            var receipt = new PaymentReceipt(first.Id, Guid.NewGuid(), Hash, "OV-LARGE", large, "VND", beforeMidnight, now);
            db.PaymentReceipts.Add(receipt);
            var cod = Order(db, cart, product, now, OrderPaymentMethod.CashOnDelivery, 3);
            cod.RecordPayment(midnight);
            db.PaymentReceipts.Add(new PaymentReceipt(cod.Id, Guid.NewGuid(), Hash, "OV-COD", 3, "VND", midnight, now, "ManualCOD"));
            var outside = Order(db, cart, product, now, price: 19);
            outside.RecordPayment(afterRange);
            db.PaymentReceipts.Add(new PaymentReceipt(outside.Id, Guid.NewGuid(), Hash, "OV-OUTSIDE", 19, "VND", afterRange, now));
            db.Refunds.Add(new Refund(receipt.Id, first.Id, Guid.NewGuid(), Hash, "OV-REFUND", 11, "VND", midnight.AddHours(2),
                beforeMidnight, now, "Verified refund", "ManualBankTransfer", actor));
            await db.SaveChangesAsync();
        }
        await using var app = await TestHostApplication.StartAsync(fixture, true);
        using var client = app.GetTestClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/overview/finance")).StatusCode);
        await LoginAsync(client);
        var response = await client.GetAsync("/api/admin/overview/finance?from=2026-01-02&to=2026-01-03");
        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.CacheControl?.NoStore);
        var finance = (await response.Content.ReadFromJsonAsync<OverviewFinanceDto>())!;
        Assert.Equal("3", finance.Received);
        Assert.Equal("11", finance.Refunded);
        Assert.Equal("-8", finance.Net);
        Assert.Equal("Asia/Ho_Chi_Minh", finance.TimeZone);
        Assert.Equal(2, finance.Days.Count);
        Assert.Equal("-8", finance.Days[0].Net);
        Assert.Equal("0", finance.Days[1].Received);
        Assert.Equal(DateTimeKind.Utc, finance.GeneratedAtUtc.Kind);
        var largeResponse = await client.GetAsync("/api/admin/overview/finance?from=2026-01-01&to=2026-01-01");
        var largeJson = await largeResponse.Content.ReadAsStringAsync();
        Assert.Contains("\"9007199254740993\"", largeJson, StringComparison.Ordinal);
        Assert.DoesNotContain("customer-private", largeJson, StringComparison.Ordinal);
        Assert.DoesNotContain("OV-LARGE", largeJson, StringComparison.Ordinal);
        Assert.Equal(7, (await client.GetFromJsonAsync<OverviewFinanceDto>("/api/admin/overview/finance"))!.Days.Count);
        foreach (var query in new[] { "from=0001-01-01&to=0001-01-01", "from=2026-01-01", "from=2026-01-02&to=2026-01-01", "from=2020-01-01&to=2020-04-01", "from=2999-01-01&to=2999-01-02" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/admin/overview/finance?" + query)).StatusCode);
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task WorkQueuesMatchListsKeepClosedVerificationAndNeverExpireOrReleaseOnRead(string provider)
    {
        await using var fixture = Database(provider);
        await fixture.CreateAsync();
        var actor = await AdminAsync(fixture);
        using (var scope = fixture.Scope(actor))
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var type = new ProductType("OVWORK", "Overview work");
            db.ProductTypes.Add(type);
            var physical = Product(db, type, "work");
            var extension = Product(db, type, "extension", ProductFulfillmentKind.ManualExtension);
            var cart = new Cart(actor);
            db.Carts.Add(cart);
            var now = DateTime.UtcNow;
            for (var index = 0; index < 7; index++) Order(db, cart, physical, now);
            Order(db, cart, physical, now.AddHours(-2)); // pending status whose hold is expired: GET must leave it alone
            Order(db, cart, physical, now, OrderPaymentMethod.CashOnDelivery);
            var partial = Order(db, cart, physical, now, OrderPaymentMethod.CashOnDelivery);
            partial.RegisterPhysicalShipment(false);
            var review = Order(db, cart, physical, now);
            review.RecordPayment(now);
            var cancelledPaid = Order(db, cart, physical, now);
            cancelledPaid.Cancel();
            cancelledPaid.RecordPayment(now);
            var closed = Order(db, cart, physical, now);
            closed.RecordPayment(now);
            closed.CloseFulfillment();
            foreach (var state in new[] { "ready", "closed-ready", "closed-verification" })
            {
                var order = Order(db, cart, extension, now);
                order.RecordPayment(now);
                order.CompleteFulfillment(false);
                var request = new RenewalRequest(order.Id, order.Items.Single().Id, extension.Variants.Single().Id);
                var targetKey = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(state)));
                request.SetTarget("PROTECTED-NOT-FOR-OVERVIEW", targetKey, "***test");
                if (state == "closed-verification")
                {
                    var token = request.Claim(actor, now, 5);
                    request.Start(actor, token, Guid.NewGuid(), now);
                }
                else if (state == "ready") request.Claim(actor, now.AddMinutes(-10), 5); // expired claim remains a queued request
                if (state.StartsWith("closed", StringComparison.Ordinal)) order.CloseFulfillment();
                db.RenewalRequests.Add(request);
            }
            await db.SaveChangesAsync();
        }
        await using var app = await TestHostApplication.StartAsync(fixture, true);
        using var client = app.GetTestClient();
        await LoginAsync(client);
        var before = await SnapshotAsync(fixture);
        var work = (await client.GetFromJsonAsync<OverviewWorkDto>("/api/admin/overview/work"))!;
        Assert.Equal(5, work.Groups.Count);
        Assert.Equal(7, work.Groups.Single(g => g.Key == "awaiting-payment").Total);
        Assert.Equal(5, work.Groups.Single(g => g.Key == "awaiting-payment").Items.Count);
        Assert.Equal(3, work.Groups.Single(g => g.Key == "awaiting-processing").Total); // two physical + active extension parent
        Assert.Equal(2, work.Groups.Single(g => g.Key == "needs-review").Total);
        Assert.Equal(1, work.Groups.Single(g => g.Key == "renewal-awaiting-work").Total);
        Assert.Equal(1, work.Groups.Single(g => g.Key == "renewal-pending-verification").Total);
        foreach (var group in work.Groups)
        {
            var endpoint = group.Kind == "order" ? "/api/admin/orders?queue=" + group.Key : "/api/admin/renewals?queue=" + group.Key[8..];
            if (group.Kind == "order")
                Assert.Equal(group.Total, (await client.GetFromJsonAsync<Page<OrderSummary>>(endpoint))!.Total);
            else
                Assert.Equal(group.Total, (await client.GetFromJsonAsync<Page<RenewalAdminDto>>(endpoint))!.Total);
            Assert.All(group.Items, item => Assert.Equal(DateTimeKind.Utc, item.CreatedAtUtc.Kind));
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/admin/orders?queue=invalid")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/admin/renewals?queue=invalid")).StatusCode);
        var json = await client.GetStringAsync("/api/admin/overview/work");
        Assert.DoesNotContain("customer-private", json, StringComparison.Ordinal);
        Assert.DoesNotContain("PROTECTED-NOT-FOR-OVERVIEW", json, StringComparison.Ordinal);
        Assert.Equal(before, await SnapshotAsync(fixture));
        foreach (var permission in new[] { "Order", "Renewal" })
        {
            await RestrictedUserAsync(fixture, permission.ToLowerInvariant(), [permission]);
            using var restricted = app.GetTestClient();
            await LoginAsync(restricted, permission.ToLowerInvariant() + "@example.test");
            var allowed = (await restricted.GetFromJsonAsync<OverviewWorkDto>("/api/admin/overview/work"))!;
            Assert.Equal(permission == "Order" ? 3 : 2, allowed.Groups.Count);
            Assert.All(allowed.Groups, group => Assert.Equal(permission.ToLowerInvariant(), group.Kind));
            Assert.Equal(HttpStatusCode.Forbidden, (await restricted.GetAsync("/api/admin/overview/finance")).StatusCode);
        }
    }

    private static async Task<string> SnapshotAsync(FoundationDatabase fixture)
    {
        using var scope = fixture.Scope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var orders = await db.Orders.AsNoTracking().OrderBy(o => o.Id).Select(o => o.Version).ToListAsync();
        var renewals = await db.RenewalRequests.AsNoTracking().OrderBy(o => o.Id).Select(o => o.Version).ToListAsync();
        return string.Join(",", orders.Concat(renewals)) + ":" + await db.Trails.CountAsync();
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task StockAvailabilityHonorsPublicationTrackingReservationsAndIndependentPermissions(string provider)
    {
        await using var fixture = Database(provider);
        await fixture.CreateAsync();
        var actor = await AdminAsync(fixture);
        using (var scope = fixture.Scope(actor))
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var type = new ProductType("OVSTOCK", "Overview stock");
            db.ProductTypes.Add(type);
            var location = new InventoryLocation("MAIN", "Main");
            db.Add(location);
            Product(db, type, "missing");
            var reserved = Product(db, type, "reserved");
            var balance = new InventoryItem(reserved.Variants.Single().Id, location.Id);
            balance.Adjust(Guid.NewGuid(), 7, "Initial test stock", actor);
            balance.Reserve(5);
            db.InventoryItems.Add(balance);
            var untracked = Product(db, type, "untracked");
            db.Entry(untracked.Variants.Single()).Property(v => v.TrackInventory).CurrentValue = false;
            Product(db, type, "draft", published: false);
            Product(db, type, "archived").ChangeStatus(ProductStatus.Archived, type);
            var inactive = Product(db, type, "inactive");
            db.Entry(inactive.Variants.Single()).Property(v => v.IsActive).CurrentValue = false;
            Product(db, type, "extension-stock", ProductFulfillmentKind.ManualExtension);
            var unique = Product(db, type, "unique", ProductFulfillmentKind.DigitalUnique);
            var cart = new Cart(actor);
            db.Carts.Add(cart);
            var uniqueOrder = Order(db, cart, unique, DateTime.UtcNow);
            for (var i = 0; i < 5; i++)
            {
                var stock = new DigitalStockItem(Guid.NewGuid(), unique.Id, unique.Variants.Single().Id, ProductFulfillmentKind.DigitalUnique, "U" + i, "PRIVATE-DIGITAL");
                if (i == 3) stock.SetAvailability(false);
                if (i == 4) stock.Reserve(uniqueOrder.Items.Single().Id);
                db.DigitalStockItems.Add(stock);
            }
            var shared = Product(db, type, "shared", ProductFulfillmentKind.DigitalShared);
            db.DigitalStockItems.Add(new DigitalStockItem(Guid.NewGuid(), shared.Id, shared.Variants.Single().Id, ProductFulfillmentKind.DigitalShared, "SHARED", "PRIVATE-SHARED"));
            var disabledShared = Product(db, type, "disabled-shared", ProductFulfillmentKind.DigitalShared);
            var disabled = new DigitalStockItem(Guid.NewGuid(), disabledShared.Id, disabledShared.Variants.Single().Id, ProductFulfillmentKind.DigitalShared, "SHARED", "PRIVATE-DISABLED");
            disabled.SetAvailability(false);
            db.DigitalStockItems.Add(disabled);
            for (var index = 0; index < 8; index++) Product(db, type, "page-" + index);
            await db.SaveChangesAsync();
        }
        await using var app = await TestHostApplication.StartAsync(fixture, true);
        using var client = app.GetTestClient();
        await LoginAsync(client);
        var stockResult = (await client.GetFromJsonAsync<OverviewStockDto>("/api/admin/overview/stock"))!;
        Assert.Equal(12, stockResult.Total);
        Assert.Equal(10, stockResult.Items.Count);
        Assert.All(stockResult.Items, item => Assert.Equal(0, item.Available));
        var secondPage = (await client.GetFromJsonAsync<OverviewStockDto>("/api/admin/overview/stock?page=2"))!;
        Assert.Equal(12, secondPage.Total);
        Assert.Equal(2, secondPage.PageNumber);
        Assert.Equal(new[] { 2, 3 }, secondPage.Items.Select(i => i.Available));
        Assert.Empty(stockResult.Items.Select(i => i.VariantId).Intersect(secondPage.Items.Select(i => i.VariantId)));
        Assert.Equal(10, (await client.GetFromJsonAsync<OverviewStockDto>("/api/admin/overview/stock?threshold=0"))!.Total);
        foreach (var query in new[] { "threshold=-1", "threshold=1000", "size=0", "size=11", "page=2147483647" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/admin/overview/stock?" + query)).StatusCode);
        var json = await client.GetStringAsync("/api/admin/overview/stock");
        Assert.DoesNotContain("PRIVATE-", json, StringComparison.Ordinal);
        await RestrictedUserAsync(fixture, "physical", ["Inventory"]);
        using var physicalClient = app.GetTestClient();
        await LoginAsync(physicalClient, "physical@example.test");
        var physicalStock = (await physicalClient.GetFromJsonAsync<OverviewStockDto>("/api/admin/overview/stock"))!;
        Assert.Equal(10, physicalStock.Total);
        Assert.All(physicalStock.Items, i => Assert.Equal(ProductFulfillmentKind.Physical, i.FulfillmentKind));
        Assert.Equal(HttpStatusCode.Forbidden, (await physicalClient.GetAsync("/api/admin/overview/finance")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await physicalClient.GetAsync("/api/admin/overview/work")).StatusCode);
        await RestrictedUserAsync(fixture, "digital", ["DigitalInventory"]);
        using var digitalClient = app.GetTestClient();
        await LoginAsync(digitalClient, "digital@example.test");
        var digitalStock = (await digitalClient.GetFromJsonAsync<OverviewStockDto>("/api/admin/overview/stock"))!;
        Assert.Equal(2, digitalStock.Total);
        Assert.All(digitalStock.Items, item => Assert.NotEqual(ProductFulfillmentKind.Physical, item.FulfillmentKind));
        await RestrictedUserAsync(fixture, "payment", ["Payment"], "Confirm");
        using var paymentClient = app.GetTestClient();
        await LoginAsync(paymentClient, "payment@example.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await paymentClient.GetAsync("/api/admin/overview/finance")).StatusCode);
        await RestrictedUserAsync(fixture, "finance", ["FinancialSummary"]);
        using var financeClient = app.GetTestClient();
        await LoginAsync(financeClient, "finance@example.test");
        Assert.Equal(HttpStatusCode.OK, (await financeClient.GetAsync("/api/admin/overview/finance")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await financeClient.GetAsync("/api/admin/overview/stock")).StatusCode);
        using (var scope = fixture.Scope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var roleId = await db.Roles.Where(r => r.Name == "finance").Select(r => r.Id).SingleAsync();
            await db.Permissions.Where(p => p.RoleId == roleId).ExecuteUpdateAsync(p => p.SetProperty(x => x.DeletedOn, DateTime.UtcNow));
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await financeClient.GetAsync("/api/admin/overview/finance")).StatusCode);
    }

    private static async Task RestrictedUserAsync(FoundationDatabase fixture, string name, string[] functions, string actionCode = "View")
    {
        using var scope = fixture.Scope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var role = new ApplicationRole(name, "Overview limited rights");
        Assert.True((await roles.CreateAsync(role)).Succeeded);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = name + "@example.test", Email = name + "@example.test", EmailConfirmed = true, IsActive = true };
        Assert.True((await users.CreateAsync(user, Password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, name)).Succeeded);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var action = await db.Actions.SingleAsync(a => a.Code == actionCode);
        foreach (var function in await db.Functions.Where(f => functions.Contains(f.Code)).ToListAsync())
            db.Permissions.Add(new Permission(role.Id, function.Id, action.Id));
        await db.SaveChangesAsync();
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task ScopedOperatorAddsOnlySelectedCapabilityAndPreservesExistingGrants(string provider)
    {
        await using var fixture = Database(provider);
        await fixture.CreateAsync();
        using var scope = fixture.Scope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        Assert.True((await roles.CreateAsync(new ApplicationRole("Admin", "Test operator"))).Succeeded);
        var bootstrap = scope.ServiceProvider.GetRequiredService<FoundationBootstrap>();
        await bootstrap.RunAsync(["--register-capabilities", "--grant-admin-capabilities", "--function", "Order"]);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var staff = new ApplicationRole("Staff", "Existing grants must survive scoped rollout");
        Assert.True((await roles.CreateAsync(staff)).Succeeded);
        var existingFunction = await db.Functions.SingleAsync(f => f.Code == "Order");
        var existingAction = await db.Actions.SingleAsync(a => a.Code == "View");
        db.Permissions.Add(new Permission(staff.Id, existingFunction.Id, existingAction.Id));
        await db.SaveChangesAsync();
        var oldGrants = await db.Permissions.Select(p => p.Id).ToListAsync();
        await bootstrap.RunAsync(["--register-capabilities", "--grant-admin-capabilities", "--function", "FinancialSummary"]);
        Assert.Equal(2, await db.Functions.CountAsync());
        Assert.False(await db.Functions.AnyAsync(f => f.Code == "StorefrontSettings"));
        Assert.Equal(oldGrants.Count + 1, await db.Permissions.CountAsync());
        Assert.All(oldGrants, id => Assert.True(db.Permissions.Any(p => p.Id == id)));
        var roleVersion = (await db.Roles.SingleAsync(r => r.Name == "Admin")).ConcurrencyStamp;
        await bootstrap.RunAsync(["--register-capabilities", "--grant-admin-capabilities", "--function", "FinancialSummary"]);
        Assert.Equal(oldGrants.Count + 1, await db.Permissions.CountAsync());
        Assert.Equal(roleVersion, (await db.Roles.SingleAsync(r => r.Name == "Admin")).ConcurrencyStamp);
        var lockVersion = await db.AuthorizationLocks.Select(l => l.Version).SingleAsync();
        foreach (var args in new[]
        {
            new[] { "--register-capabilities", "--function" },
            new[] { "--register-capabilities", "--function", "Unknown" },
            new[] { "--register-capabilities", "--function", "Order", "--function", "FinancialSummary" },
            new[] { "--bootstrap-admin", "--function", "FinancialSummary" },
            new[] { "--preflight", "--function", "FinancialSummary" },
            new[] { "--seed-dev", "--function", "FinancialSummary" },
            new[] { "--function", "FinancialSummary" }
        })
            await Assert.ThrowsAsync<InvalidOperationException>(() => bootstrap.RunAsync(args));
        fixture.Configuration["Modules:Catalog:Enabled"] = "false";
        await Assert.ThrowsAsync<InvalidOperationException>(() => bootstrap.RunAsync(["--register-capabilities", "--function", "FinancialSummary"]));
        Assert.Equal(lockVersion, await db.AuthorizationLocks.Select(l => l.Version).SingleAsync());
    }
}
