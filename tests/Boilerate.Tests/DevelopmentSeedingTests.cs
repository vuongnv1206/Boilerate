using Boilerate.Application.Catalog;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Identity;
using Boilerate.Infrastructure.Persistence.Context;
using Boilerate.Infrastructure.Persistence.Initialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Boilerate.Tests;

[Collection("Foundation")]
public sealed class DevelopmentSeedingTests
{
    [Fact]
    public async Task RunnerAwaitsSeedersInRegistrationOrder()
    {
        var events = new List<string>();
        ICustomSeeder Seed(string name) => new Seeder(async token =>
        {
            events.Add($"{name}:start");
            await Task.Yield();
            token.ThrowIfCancellationRequested();
            events.Add($"{name}:end");
        });
        await Runner([Seed("first"), Seed("second")]).RunAsync();
        Assert.Equal(["first:start", "first:end", "second:start", "second:end"], events);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task RunnerRejectsNonDevelopmentBeforeCallingAnySeeder(string environment)
    {
        var called = false;
        var seeder = new Seeder(_ => { called = true; return Task.CompletedTask; });
        await Assert.ThrowsAsync<InvalidOperationException>(() => Runner([seeder], environment).RunAsync());
        Assert.False(called);
    }

    [Fact]
    public async Task CatalogDataIsReadFromTheCopiedJsonFile()
    {
        var data = await CatalogSeedData.ReadAsync(default);
        Assert.Equal(2, data.Types.Length);
        Assert.Equal(6, data.Categories.Length);
        Assert.Equal(14, data.Products.Length);
        Assert.Contains(data.Products, product => product.Sku == "DEMO-ACC-001" && product.Price == 690000);
    }

    [Fact]
    public async Task OrderingDataIsReadFromTheCopiedJsonFile()
    {
        var data = await OrderingSeedData.ReadAsync(default);
        Assert.Equal(8, data.Scenarios.Length);
        Assert.Contains(data.Scenarios, scenario => scenario.Key == "prepaid-needs-review" && scenario.Action == "ConfirmAndReview");
        Assert.Contains(data.Scenarios, scenario => scenario.Lines.Length == 2);
        var catalog = await CatalogSeedData.ReadAsync(default);
        var publishedSkus = catalog.Products.Where(product => product.Status == ProductStatus.Published)
            .Select(product => product.Sku).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.All(data.Scenarios.SelectMany(scenario => scenario.Lines), line => Assert.Contains(line.Sku, publishedSkus));
    }

    [Fact]
    public async Task CancellationIsForwardedAndStopsBeforeTheNextSeeder()
    {
        using var cancellation = new CancellationTokenSource();
        var nextCalled = false;
        var first = new Seeder(token =>
        {
            Assert.Equal(cancellation.Token, token);
            cancellation.Cancel();
            return Task.CompletedTask;
        });
        var second = new Seeder(_ => { nextCalled = true; return Task.CompletedTask; });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Runner([first, second]).RunAsync(cancellation.Token));
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task SeederFailureStopsTheSequenceAndRemainsVisibleToTheOperator()
    {
        var nextCalled = false;
        var failure = new InvalidOperationException("Sample write failed");
        var first = new Seeder(_ => Task.FromException(failure));
        var second = new Seeder(_ => { nextCalled = true; return Task.CompletedTask; });
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => Runner([first, second]).RunAsync()));
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task RegistrationIsScopedAndDoesNotRequireTheCatalogModule()
    {
        await using var withoutCatalog = new FoundationDatabase("mssql");
        using (var scope = withoutCatalog.Scope())
        {
            Assert.Empty(scope.ServiceProvider.GetServices<ICustomSeeder>());
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<CustomSeederRunner>());
        }
        await using var withCatalog = new FoundationDatabase("mssql", catalog: true);
        using var first = withCatalog.Scope();
        using var second = withCatalog.Scope();
        var seeders = first.ServiceProvider.GetServices<ICustomSeeder>().ToArray();
        Assert.Collection(seeders,
            item => Assert.IsType<CatalogDevelopmentSeeder>(item),
            item => Assert.IsType<OrderingDevelopmentSeeder>(item));
        Assert.Same(seeders[0], first.ServiceProvider.GetServices<ICustomSeeder>().First());
        Assert.NotSame(seeders[0], second.ServiceProvider.GetServices<ICustomSeeder>().First());
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task CatalogSeedRunsThroughUseCasesAndReplayPreservesUserEdits(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, catalog: true,
            configOverrides: new Dictionary<string, string?> { ["DevelopmentSeed:ActorEmail"] = "seeder@example.test" });
        fixture.Services.GetRequiredService<IHostEnvironment>().EnvironmentName = Environments.Development;
        await fixture.CreateAsync();
        await BootstrapAsync(fixture);
        using (var scope = fixture.Scope())
        {
            await scope.ServiceProvider.GetRequiredService<CustomSeederRunner>().RunAsync();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(2, await db.ProductTypes.CountAsync());
            Assert.Equal(6, await db.Categories.CountAsync());
            Assert.Equal(14, await db.Products.CountAsync());
            Assert.Equal(14, await db.ProductVariants.CountAsync());
            Assert.Equal(14, await db.InventoryItems.CountAsync());
            Assert.Equal(16, await db.StockMovements.CountAsync());
            Assert.Equal(8, await db.Orders.CountAsync());
            Assert.Equal(3, await db.PaymentReceipts.CountAsync());
            Assert.Equal(2, await db.Shipments.CountAsync());
            Assert.Equal(8, await db.Products.CountAsync(product => product.Status == ProductStatus.Published));
            Assert.All(await db.StockMovements.ToListAsync(), movement => Assert.NotEqual(Guid.Empty, movement.ActorId));

            var service = scope.ServiceProvider.GetRequiredService<ICommerceService>();
            var firstId = await db.Products.Where(product => product.Slug == "tui-everyday-tote").Select(product => product.Id).SingleAsync();
            var first = await service.GetAsync(firstId, false, default);
            var type = await service.TypeAsync(first.ProductTypeId, default);
            first = await service.EditAsync(new(first.Id, first.Version, type.Version,
                new("Tên do quản trị sửa", "slug-da-thay-doi", "Nội dung đã chỉnh", first.CategoryIds.ToList(), [], [])), default);
            first = await service.EditVariantAsync(new(first.Id, first.Variants.Single().Id, first.Version, 123000, null, true, null), default);
            await service.StatusAsync(new(first.Id, first.Version, ProductStatus.Archived), default);
            var inventory = await db.InventoryItems.AsNoTracking().SingleAsync(item => item.VariantId == first.Variants.Single().Id);
            await service.AdjustAsync(new(inventory.Id, inventory.Version, Guid.NewGuid(), -1, "Operator correction"), default);
        }
        using (var scope = fixture.Scope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var before = await db.Products.AsNoTracking().ToDictionaryAsync(product => product.Id, product => product.Version);
            var ordersBefore = await db.Orders.AsNoTracking().ToDictionaryAsync(order => order.Id, order => order.Version);
            var grantsBefore = await db.Permissions.CountAsync();
            await scope.ServiceProvider.GetRequiredService<CustomSeederRunner>().RunAsync();
            Assert.Equal(14, await db.Products.CountAsync());
            Assert.Equal(6, await db.Categories.CountAsync());
            Assert.Equal(2, await db.ProductTypes.CountAsync());
            Assert.Equal(17, await db.StockMovements.CountAsync());
            Assert.Equal(8, await db.Orders.CountAsync());
            Assert.Equal(3, await db.PaymentReceipts.CountAsync());
            Assert.Equal(2, await db.Shipments.CountAsync());
            var ordersAfter = await db.Orders.AsNoTracking().ToDictionaryAsync(order => order.Id, order => order.Version);
            Assert.Equal(ordersBefore.OrderBy(pair => pair.Key), ordersAfter.OrderBy(pair => pair.Key));
            Assert.Equal(grantsBefore, await db.Permissions.CountAsync());
            var after = await db.Products.AsNoTracking().ToDictionaryAsync(product => product.Id, product => product.Version);
            Assert.Equal(before.OrderBy(pair => pair.Key), after.OrderBy(pair => pair.Key));
            var edited = await db.Products.Include(product => product.Variants).SingleAsync(product => product.Slug == "slug-da-thay-doi");
            Assert.Equal("Tên do quản trị sửa", edited.Name);
            Assert.Equal(ProductStatus.Archived, edited.Status);
            Assert.Equal(123000, edited.Variants.Single().Price);
            Assert.Equal(11, (await db.InventoryItems.SingleAsync(item => item.VariantId == edited.Variants.Single().Id)).OnHand);
            Assert.False(db.Database.HasPendingModelChanges());
        }
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task ActorWithoutExistingGrantsIsRejectedBeforeCatalogWrites(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, catalog: true,
            configOverrides: new Dictionary<string, string?> { ["DevelopmentSeed:ActorEmail"] = "unprivileged@example.test" });
        fixture.Services.GetRequiredService<IHostEnvironment>().EnvironmentName = Environments.Development;
        await fixture.CreateAsync();
        using var scope = fixture.Scope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var result = await users.CreateAsync(new ApplicationUser
        {
            UserName = "unprivileged@example.test", Email = "unprivileged@example.test", IsActive = true, EmailConfirmed = true
        });
        Assert.True(result.Succeeded);
        await Assert.ThrowsAsync<ForbiddenException>(() => scope.ServiceProvider.GetRequiredService<CustomSeederRunner>().RunAsync());
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Empty(await db.Products.ToListAsync());
        Assert.Empty(await db.ProductTypes.ToListAsync());
        Assert.Empty(await db.Categories.ToListAsync());
        Assert.Empty(await db.Permissions.ToListAsync());
    }

    private static CustomSeederRunner Runner(IEnumerable<ICustomSeeder> seeders, string environment = "Development") =>
        new(seeders, new HostEnvironment { EnvironmentName = environment }, NullLogger<CustomSeederRunner>.Instance);

    private static async Task BootstrapAsync(FoundationDatabase fixture)
    {
        var previousEmail = Environment.GetEnvironmentVariable("BOILERATE_BOOTSTRAP_EMAIL");
        var previousPassword = Environment.GetEnvironmentVariable("BOILERATE_BOOTSTRAP_PASSWORD");
        try
        {
            Environment.SetEnvironmentVariable("BOILERATE_BOOTSTRAP_EMAIL", "seeder@example.test");
            Environment.SetEnvironmentVariable("BOILERATE_BOOTSTRAP_PASSWORD", "IsolatedSeedTestPassword!482");
            using var scope = fixture.Scope();
            await scope.ServiceProvider.GetRequiredService<FoundationBootstrap>().RunAsync(["--bootstrap-admin"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable("BOILERATE_BOOTSTRAP_EMAIL", previousEmail);
            Environment.SetEnvironmentVariable("BOILERATE_BOOTSTRAP_PASSWORD", previousPassword);
        }
    }

    private sealed record Seeder(Func<CancellationToken, Task> Run) : ICustomSeeder
    {
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Run(cancellationToken);
    }

    private sealed class HostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Boilerate.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
