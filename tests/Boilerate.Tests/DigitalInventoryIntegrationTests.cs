using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Boilerate.Application.Catalog;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Identity.Tokens;
using Boilerate.Application.Identity.Users;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using Boilerate.Domain.DigitalInventory;
using Boilerate.Domain.Inventory;
using Boilerate.Infrastructure.Commerce;
using Boilerate.Infrastructure.Persistence.Context;
using Boilerate.Infrastructure.Persistence.Initialization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Boilerate.Tests;

[Collection("Foundation")]
public sealed class DigitalInventoryIntegrationTests
{
    private static CommerceService Service(IServiceScope scope, string? deniedPermission = null)
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = Substitute.For<IUserService>();
        users.HasPermissionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => (string)call[1] != deniedPermission);
        return new(db, new CommerceUnitOfWork(db), scope.ServiceProvider.GetRequiredService<ICurrentUser>(), users,
            Options.Create(new CommerceSettings()), scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>());
    }

    private static CreateProduct Simple(ProductType type, string slug, ProductFulfillmentKind kind) =>
        new(type.Id, type.Version, new("Product " + slug, slug, null, [], [], []), [], [new(slug, 100, null, new())], kind);

    private static async Task<ProductDto> Publish(CommerceService service, ProductDto product) =>
        await service.StatusAsync(new(product.Id, product.Version, ProductStatus.Published), default);

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task FulfillmentUsesSeparateStockSourcesAndPublicAvailability(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var service = Service(scope);
        await service.InitializeAsync(default);
        var type = await service.CreateTypeAsync(new("FLEX", "Flexible", []), default);
        var products = new Dictionary<ProductFulfillmentKind, ProductDto>();
        foreach (var kind in Enum.GetValues<ProductFulfillmentKind>())
        {
            var product = await service.CreateAsync(Simple(type, "mode-" + kind.ToString().ToLowerInvariant(), kind), default);
            Assert.Equal(kind, product.FulfillmentKind);
            products[kind] = await Publish(service, product);
        }
        var physical = products[ProductFulfillmentKind.Physical];
        var unique = products[ProductFulfillmentKind.DigitalUnique];
        var shared = products[ProductFulfillmentKind.DigitalShared];
        var extension = products[ProductFulfillmentKind.ManualExtension];
        var physicalInventory = Assert.Single((await service.InventoryAsync(1, 20, default)).Items);
        Assert.Equal(physical.Variants.Single().Id, physicalInventory.VariantId);
        Assert.Empty(await db.StockMovements.ToListAsync());
        Assert.False((await service.GetBySlugAsync(physical.Slug, default)).Variants.Single().InStock);
        Assert.False((await service.GetBySlugAsync(unique.Slug, default)).Variants.Single().InStock);
        Assert.False((await service.GetBySlugAsync(shared.Slug, default)).Variants.Single().InStock);
        Assert.True((await service.GetBySlugAsync(extension.Slug, default)).Variants.Single().InStock);

        var imported = Assert.Single(await service.ImportDigitalAsync(new(unique.Id, unique.Variants.Single().Id, unique.Version,
            [new("CODE-1", "activation-secret-1")]), default));
        var sharedStock = await service.SaveSharedAsync(new(shared.Id, shared.Variants.Single().Id, shared.Version, "https://course.example.test/private"), default);
        await service.AdjustAsync(new(physicalInventory.Id, physicalInventory.Version, Guid.NewGuid(), 1, "Initial receipt"), default);
        var available = await service.SearchAsync(new() { InStock = true }, true, default);
        Assert.Equal(4, available.Total);
        Assert.Equal(4, available.Items.Count);
        Assert.All(available.Items, product => Assert.Equal(products.Values.Single(p => p.Id == product.Id).FulfillmentKind, product.FulfillmentKind));
        Assert.True((await service.GetBySlugAsync(unique.Slug, default)).Variants.Single().InStock);
        Assert.True((await service.GetBySlugAsync(shared.Slug, default)).Variants.Single().InStock);

        await service.SetDigitalStockAvailabilityAsync(new(unique.Id, unique.Variants.Single().Id, imported.Id, imported.ProductVersion, false), default);
        await service.SetDigitalStockAvailabilityAsync(new(shared.Id, shared.Variants.Single().Id, sharedStock.Id, sharedStock.ProductVersion, false), default);
        var unavailable = await service.SearchAsync(new() { InStock = false }, true, default);
        Assert.Equal(2, unavailable.Total);
        Assert.Equal(new[] { unique.Id, shared.Id }.Order(), unavailable.Items.Select(p => p.Id).Order());
        Assert.False((await service.GetBySlugAsync(unique.Slug, default)).Variants.Single().InStock);
        Assert.False((await service.GetBySlugAsync(shared.Slug, default)).Variants.Single().InStock);

        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task SecretsAreProtectedAndOnlyExplicitAuthorizedRevealIsAudited(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        var actorId = Guid.NewGuid();
        using var scope = fixture.Scope(actorId);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var service = Service(scope);
        await service.InitializeAsync(default);
        var type = await service.CreateTypeAsync(new("SECRETS", "Secrets", []), default);
        var product = await service.CreateAsync(Simple(type, "secret-account", ProductFulfillmentKind.DigitalUnique), default);
        const string secret = "user@example.test\nPassword: KeepThisSecret!1122\n  Mã riêng: đừng cắt khoảng trắng  ";
        var metadata = Assert.Single(await service.ImportDigitalAsync(new(product.Id, product.Variants.Single().Id, product.Version,
            [new("account-001", secret)]), default));
        Assert.Equal("ACCOUNT-001", metadata.Reference);
        var row = await db.DigitalStockItems.AsNoTracking().SingleAsync();
        Assert.NotEqual(secret, row.ProtectedContent);
        Assert.DoesNotContain("KeepThisSecret", row.ProtectedContent);
        Assert.DoesNotContain("KeepThisSecret", JsonSerializer.Serialize(await service.DigitalStockAsync(row.VariantId, 1, 20, default)));
        Assert.DoesNotContain(row.ProtectedContent, JsonSerializer.Serialize(metadata));
        Assert.DoesNotContain("ProtectedContent", JsonSerializer.Serialize(metadata));
        product = await service.GetAsync(product.Id, false, default);
        product = await Publish(service, product);
        var publicJson = JsonSerializer.Serialize(await service.GetBySlugAsync(product.Slug, default));
        Assert.DoesNotContain("KeepThisSecret", publicJson);
        Assert.DoesNotContain(row.ProtectedContent, publicJson);
        var beforeReveal = await db.Trails.CountAsync();
        var denied = Service(scope, "Permissions.DigitalInventory.Reveal");
        await Assert.ThrowsAsync<ForbiddenException>(() => denied.RevealDigitalStockAsync(row.Id, default));
        Assert.Equal(beforeReveal, await db.Trails.CountAsync());

        var revealed = await service.RevealDigitalStockAsync(row.Id, default);
        Assert.Equal(row.Id, revealed.Id);
        Assert.Equal(secret, revealed.Content);
        var trails = await db.Trails.AsNoTracking().OrderBy(t => t.DateTime).ToListAsync();
        Assert.Equal(beforeReveal + 1, trails.Count);
        var revealTrail = trails.Last();
        Assert.Equal(actorId, revealTrail.UserId);
        Assert.Contains(row.Id.ToString(), revealTrail.PrimaryKey);
        foreach (var trail in trails)
        {
            var serialized = JsonSerializer.Serialize(trail);
            Assert.DoesNotContain("KeepThisSecret", serialized);
            Assert.DoesNotContain(row.ProtectedContent, serialized);
            Assert.DoesNotContain("ProtectedContent", serialized);
        }

        db.DigitalStockItems.Remove(await db.DigitalStockItems.SingleAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task ImportsRejectDuplicatesStaleVersionsWrongOwnersAndModesWithoutPartialWrites(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var service = Service(scope);
        await service.InitializeAsync(default);
        var type = await service.CreateTypeAsync(new("IMPORT", "Import", []), default);
        var product = await service.CreateAsync(Simple(type, "import", ProductFulfillmentKind.DigitalUnique), default);
        var variantId = product.Variants.Single().Id;
        var other = await service.CreateAsync(Simple(type, "other", ProductFulfillmentKind.DigitalUnique), default);
        var physical = await service.CreateAsync(Simple(type, "physical", ProductFulfillmentKind.Physical), default);
        var shared = await service.CreateAsync(Simple(type, "shared", ProductFulfillmentKind.DigitalShared), default);
        var extension = await service.CreateAsync(Simple(type, "extension", ProductFulfillmentKind.ManualExtension), default);
        var stock = Assert.Single(await service.ImportDigitalAsync(new(product.Id, variantId, product.Version, [new("code-1", "first")]), default));
        Assert.NotEqual(product.Version, stock.ProductVersion);
        await Assert.ThrowsAsync<ConflictException>(() => service.ImportDigitalAsync(new(product.Id, variantId, product.Version, [new("stale", "stale")]), default));
        product = await service.GetAsync(product.Id, false, default);
        var stableVersion = product.Version;
        var trailCount = await db.Trails.CountAsync();
        await Assert.ThrowsAsync<ConflictException>(() => service.ImportDigitalAsync(new(product.Id, variantId, stableVersion,
            [new("new-before-duplicate", "new"), new("CODE-1", "duplicate")]), default));
        await Assert.ThrowsAsync<ConflictException>(() => service.ImportDigitalAsync(new(product.Id, variantId, stableVersion,
            [new("case-duplicate", "a"), new("CASE-DUPLICATE", "b")]), default));
        await Assert.ThrowsAsync<DomainException>(() => service.ImportDigitalAsync(new(product.Id, variantId, stableVersion,
            [new("valid-before-invalid", "valid"), new("invalid", " ")]), default));
        var wrongOwner = await Record.ExceptionAsync(() => service.ImportDigitalAsync(new(product.Id, other.Variants.Single().Id, stableVersion, [new("wrong-owner", "secret")]), default));
        Assert.True(wrongOwner is DomainException or NotFoundException, wrongOwner?.ToString() ?? "Cross-product import succeeded.");
        foreach (var wrongMode in new[] { physical, shared, extension })
            await Assert.ThrowsAsync<DomainException>(() => service.ImportDigitalAsync(new(wrongMode.Id, wrongMode.Variants.Single().Id, wrongMode.Version, [new("wrong-mode", "secret")]), default));
        Assert.Single(await db.DigitalStockItems.AsNoTracking().ToListAsync());
        Assert.Equal(stableVersion, (await service.GetAsync(product.Id, false, default)).Version);
        Assert.Equal(trailCount, await db.Trails.CountAsync());

        // Even an invalid legacy balance cannot route a digital SKU through physical adjustment.
        var main = await db.Set<InventoryLocation>().SingleAsync();
        var invalidBalance = new InventoryItem(variantId, main.Id);
        db.InventoryItems.Add(invalidBalance);
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ConflictException>(() => service.AdjustAsync(new(invalidBalance.Id, invalidBalance.Version, Guid.NewGuid(), 1, "Wrong stock source"), default));
        Assert.Equal(0, (await db.InventoryItems.AsNoTracking().SingleAsync(i => i.Id == invalidBalance.Id)).OnHand);
        Assert.Empty(await db.StockMovements.ToListAsync());
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task SharedContentUpdatesOneRowAndAllDigitalServiceOperationsEnforcePermissions(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var service = Service(scope);
        await service.InitializeAsync(default);
        var type = await service.CreateTypeAsync(new("SHARED", "Shared", []), default);
        var product = await service.CreateAsync(Simple(type, "shared-link", ProductFulfillmentKind.DigitalShared), default);
        var variantId = product.Variants.Single().Id;
        var first = await service.SaveSharedAsync(new(product.Id, variantId, product.Version, "https://example.test/first"), default);
        var second = await service.SaveSharedAsync(new(product.Id, variantId, first.ProductVersion, "https://example.test/second", false), default);
        Assert.Equal(first.Id, second.Id);
        Assert.NotEqual(first.Version, second.Version);
        Assert.NotEqual(first.ProductVersion, second.ProductVersion);
        Assert.Equal("SHARED", second.Reference);
        Assert.Equal(DigitalStockState.Disabled, second.State);
        Assert.Single(await db.DigitalStockItems.ToListAsync());
        Assert.Equal("https://example.test/second", (await service.RevealDigitalStockAsync(second.Id, default)).Content);
        await Assert.ThrowsAsync<ConflictException>(() => service.SaveSharedAsync(new(product.Id, variantId, first.ProductVersion, "must-not-replace"), default));
        Assert.Equal("https://example.test/second", (await service.RevealDigitalStockAsync(second.Id, default)).Content);

        await Assert.ThrowsAsync<ForbiddenException>(() => Service(scope, "Permissions.DigitalInventory.View").DigitalStockAsync(variantId, 1, 20, default));
        await Assert.ThrowsAsync<ForbiddenException>(() => Service(scope, "Permissions.DigitalInventory.Reveal").RevealDigitalStockAsync(second.Id, default));
        await Assert.ThrowsAsync<ForbiddenException>(() => Service(scope, "Permissions.DigitalInventory.Update").SaveSharedAsync(new(product.Id, variantId, second.ProductVersion, "denied"), default));
        await Assert.ThrowsAsync<ForbiddenException>(() => Service(scope, "Permissions.DigitalInventory.Update").SetDigitalStockAvailabilityAsync(new(product.Id, variantId, second.Id, second.ProductVersion, true), default));
        var unique = await service.CreateAsync(Simple(type, "unique-permission", ProductFulfillmentKind.DigitalUnique), default);
        await Assert.ThrowsAsync<ForbiddenException>(() => Service(scope, "Permissions.DigitalInventory.Import").ImportDigitalAsync(new(unique.Id, unique.Variants.Single().Id, unique.Version, [new("denied", "denied")]), default));
        Assert.Single(await db.DigitalStockItems.AsNoTracking().ToListAsync());
        Assert.Equal(DigitalStockState.Disabled, (await service.DigitalStockAsync(variantId, 1, 20, default)).Items.Single().State);
        var enabled = await service.SetDigitalStockAvailabilityAsync(new(product.Id, variantId, second.Id, second.ProductVersion, true), default);
        Assert.Equal(DigitalStockState.Available, enabled.State);
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task DatabaseRejectsCrossProductModeDuplicateAndInvalidStateRelations(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var service = Service(scope);
        await service.InitializeAsync(default);
        var type = await service.CreateTypeAsync(new("CONSTRAINT", "Constraints", []), default);
        var first = await service.CreateAsync(Simple(type, "constraint-first", ProductFulfillmentKind.DigitalUnique), default);
        var other = await service.CreateAsync(Simple(type, "constraint-other", ProductFulfillmentKind.DigitalUnique), default);
        var shared = await service.CreateAsync(Simple(type, "constraint-shared", ProductFulfillmentKind.DigitalShared), default);
        var rows = await service.ImportDigitalAsync(new(first.Id, first.Variants.Single().Id, first.Version, [new("A", "first"), new("B", "second")]), default);
        var sharedRow = await service.SaveSharedAsync(new(shared.Id, shared.Variants.Single().Id, shared.Version, "https://example.test/shared"), default);
        var rowId = rows.First().Id;
        var otherRowId = rows.Last().Id;
        async Task Reject(FormattableString sql)
        {
            var error = await Record.ExceptionAsync(() => db.Database.ExecuteSqlInterpolatedAsync(sql));
            Assert.True(error is Microsoft.Data.SqlClient.SqlException { Number: 547 or 2601 or 2627 } ||
                error is Npgsql.PostgresException { SqlState: "23503" or "23505" or "23514" }, error?.ToString() ?? "Constraint was not enforced.");
        }
        await Reject($"UPDATE \"DigitalInventory\".\"DigitalStockItems\" SET \"VariantId\"={other.Variants.Single().Id} WHERE \"Id\"={rowId}");
        await Reject($"UPDATE \"DigitalInventory\".\"DigitalStockItems\" SET \"FulfillmentKind\"={3} WHERE \"Id\"={rowId}");
        await Reject($"UPDATE \"DigitalInventory\".\"DigitalStockItems\" SET \"State\"={999} WHERE \"Id\"={rowId}");
        await Reject($"UPDATE \"DigitalInventory\".\"DigitalStockItems\" SET \"FulfillmentKind\"={1} WHERE \"Id\"={rowId}");
        await Reject($"UPDATE \"DigitalInventory\".\"DigitalStockItems\" SET \"Reference\"={rows.First().Reference} WHERE \"Id\"={otherRowId}");
        await Reject($"UPDATE \"DigitalInventory\".\"DigitalStockItems\" SET \"ProductId\"={shared.Id},\"VariantId\"={shared.Variants.Single().Id},\"FulfillmentKind\"={3} WHERE \"Id\"={rowId}");
        await Reject($"DELETE FROM \"Catalog\".\"ProductVariants\" WHERE \"Id\"={shared.Variants.Single().Id}");
        Assert.Equal(3, await db.DigitalStockItems.CountAsync());
        Assert.Equal(DigitalStockState.Available, (await db.DigitalStockItems.AsNoTracking().SingleAsync(i => i.Id == sharedRow.Id)).State);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task HttpDigitalEndpointsRequireIndependentCapabilitiesAndNeverCacheSecrets(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        var oldEmail = Environment.GetEnvironmentVariable("BOILERATE_BOOTSTRAP_EMAIL");
        var oldPassword = Environment.GetEnvironmentVariable("BOILERATE_BOOTSTRAP_PASSWORD");
        try
        {
            Environment.SetEnvironmentVariable("BOILERATE_BOOTSTRAP_EMAIL", "digital@example.test");
            Environment.SetEnvironmentVariable("BOILERATE_BOOTSTRAP_PASSWORD", "DigitalPassword!29874");
            using var bootstrap = fixture.Scope();
            await bootstrap.ServiceProvider.GetRequiredService<FoundationBootstrap>().RunAsync(["--bootstrap-admin"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable("BOILERATE_BOOTSTRAP_EMAIL", oldEmail);
            Environment.SetEnvironmentVariable("BOILERATE_BOOTSTRAP_PASSWORD", oldPassword);
        }
        ProductDto product;
        DigitalStockDto stock;
        using (var scope = fixture.Scope())
        {
            var service = Service(scope);
            await service.InitializeAsync(default);
            var type = await service.CreateTypeAsync(new("HTTPDIGITAL", "HTTP Digital", []), default);
            product = await service.CreateAsync(Simple(type, "http-digital", ProductFulfillmentKind.DigitalUnique), default);
            stock = Assert.Single(await service.ImportDigitalAsync(new(product.Id, product.Variants.Single().Id, product.Version,
                [new("HTTP-1", "HttpOnlySecret!4897")]), default));
        }
        await using var app = await TestHostApplication.StartAsync(fixture, true);
        using var client = app.GetTestClient();
        var variantId = product.Variants.Single().Id;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync($"/api/digital-inventory/items/{stock.Id}/reveal", null)).StatusCode);
        var login = await client.PostAsJsonAsync("/api/tokens/get", new TokenRequest("digital@example.test", "DigitalPassword!29874"));
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<TokenResponse>())!;
        client.DefaultRequestHeaders.Authorization = new("Bearer", token.Token);
        var response = await client.GetAsync($"/api/digital-inventory/variants/{variantId}");
        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.DoesNotContain("HttpOnlySecret", await response.Content.ReadAsStringAsync());
        response = await client.PostAsync($"/api/digital-inventory/items/{stock.Id}/reveal", null);
        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal("HttpOnlySecret!4897", (await response.Content.ReadFromJsonAsync<DigitalSecretDto>())!.Content);

        using (var scope = fixture.Scope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var functionId = await db.Functions.Where(f => f.Code == "DigitalInventory").Select(f => f.Id).SingleAsync();
            var revealActionId = await db.Actions.Where(a => a.Code == "Reveal").Select(a => a.Id).SingleAsync();
            await db.Permissions.Where(p => p.FunctionId == functionId && p.ActionId == revealActionId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.DeletedOn, DateTime.UtcNow));
        }
        response = await client.PostAsync($"/api/digital-inventory/items/{stock.Id}/reveal", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        response = await client.GetAsync($"/api/digital-inventory/variants/{variantId}");
        response.EnsureSuccessStatusCode();
        var mismatch = new ImportDigitalStock(product.Id, Guid.NewGuid(), stock.ProductVersion, [new("HTTP-2", "MustNotLeak")]);
        response = await client.PostAsJsonAsync($"/api/digital-inventory/variants/{variantId}/items", mismatch);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task ConcurrentImportsRequireAFreshProductVersion(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        ProductDto product;
        using (var scope = fixture.Scope())
        {
            var service = Service(scope);
            var type = await service.CreateTypeAsync(new("CONCURRENT", "Concurrent", []), default);
            product = await service.CreateAsync(Simple(type, "concurrent", ProductFulfillmentKind.DigitalUnique), default);
        }
        async Task<bool> Import(string reference)
        {
            using var scope = fixture.Scope();
            try
            {
                await Service(scope).ImportDigitalAsync(new(product.Id, product.Variants.Single().Id, product.Version,
                    [new(reference, "concurrent-secret")]), default);
                return true;
            }
            catch (ConflictException) { return false; }
        }
        Assert.Single((await Task.WhenAll(Import("FIRST"), Import("SECOND"))).Where(success => success));
        using var verification = fixture.Scope();
        var db = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await db.DigitalStockItems.CountAsync());
        Assert.Equal(1, await db.Trails.CountAsync(t => t.TableName == "DigitalStockItems"));
        Assert.NotEqual(product.Version, (await db.Products.AsNoTracking().SingleAsync()).Version);
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task AdditiveMigrationPreservesExistingPhysicalCatalogAndBalance(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync(migrate: false);
        using var scope = fixture.Scope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var migrator = db.GetService<IMigrator>();
        var catalogMigration = db.Database.GetMigrations().Single(name => name.EndsWith("_CommerceCatalog", StringComparison.Ordinal));
        await migrator.MigrateAsync(catalogMigration);
        var typeId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var balanceId = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var version = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;
        // The previous schema has no FulfillmentKind, so seed with parameterized SQL.
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Catalog\".\"ProductTypes\" (\"Id\",\"Code\",\"NormalizedCode\",\"Name\",\"IsActive\",\"CreatedBy\",\"CreatedOn\",\"LastModifiedBy\",\"Version\") VALUES ({typeId},{"OLD"},{"OLD"},{"Existing type"},{true},{actor},{now},{actor},{version})");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Catalog\".\"Products\" (\"Id\",\"ProductTypeId\",\"Name\",\"Slug\",\"NormalizedSlug\",\"Status\",\"CreatedBy\",\"CreatedOn\",\"LastModifiedBy\",\"Version\") VALUES ({productId},{typeId},{"Existing product"},{"existing-product"},{"existing-product"},{2},{actor},{now},{actor},{version})");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Catalog\".\"ProductVariants\" (\"Id\",\"ProductId\",\"Sku\",\"NormalizedSku\",\"CombinationKey\",\"Price\",\"Currency\",\"IsDefault\",\"IsActive\") VALUES ({variantId},{productId},{"OLD-SKU"},{"OLD-SKU"},{""},{123000m},{"VND"},{true},{true})");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Inventory\".\"InventoryLocations\" (\"Id\",\"Code\",\"Name\",\"IsActive\",\"CreatedBy\",\"CreatedOn\",\"LastModifiedBy\",\"Version\") VALUES ({locationId},{"MAIN"},{"Main warehouse"},{true},{actor},{now},{actor},{version})");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Inventory\".\"InventoryItems\" (\"Id\",\"VariantId\",\"LocationId\",\"OnHand\",\"CreatedBy\",\"CreatedOn\",\"LastModifiedBy\",\"Version\") VALUES ({balanceId},{variantId},{locationId},{7},{actor},{now},{actor},{version})");
        await migrator.MigrateAsync();
        var product = await db.Products.AsNoTracking().SingleAsync();
        Assert.Equal(productId, product.Id);
        Assert.Equal("Existing product", product.Name);
        Assert.Equal(ProductFulfillmentKind.Physical, product.FulfillmentKind);
        Assert.Equal(version, product.Version);
        Assert.Equal(123000m, (await db.ProductVariants.AsNoTracking().SingleAsync()).Price);
        Assert.Equal("OLD-SKU", (await db.ProductVariants.AsNoTracking().SingleAsync()).Sku);
        Assert.Equal(7, (await db.InventoryItems.AsNoTracking().SingleAsync()).OnHand);
        Assert.Empty(await db.DigitalStockItems.ToListAsync());
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
