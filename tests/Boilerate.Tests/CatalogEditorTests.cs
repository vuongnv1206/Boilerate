using System.Net;
using System.Net.Http.Json;
using Boilerate.Application.Catalog;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Identity.Tokens;
using Boilerate.Application.Identity.Users;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using Boilerate.Domain.Media;
using Boilerate.Infrastructure.Commerce;
using Boilerate.Infrastructure.Persistence.Context;
using Boilerate.Infrastructure.Persistence.Initialization;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Boilerate.Tests;

[Collection("Foundation")]
public sealed class CatalogEditorTests
{
    private static CommerceService Service(IServiceScope scope, bool price = true)
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = Substitute.For<IUserService>();
        users.HasPermissionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(price);
        return new(db, new CommerceUnitOfWork(db), scope.ServiceProvider.GetRequiredService<ICurrentUser>(), users, Options.Create(new CommerceSettings()),
            scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>());
    }
    private static ProductContent Content(string slug) => new(slug, slug, null, [], [], []);
    private static SaveProductEditor Save(ProductDto p, ProductType type) => new(p.Id, p.Version, type.Version,
        new(p.Name, p.Slug, p.Description, p.CategoryIds.ToList(), [], p.Media.ToList()), [], [], [], p.Variants.Single(v => v.IsDefault).Sku);
    private static async Task<MediaAsset> Asset(ApplicationDbContext db)
    {
        var asset = new MediaAsset(".png");
        asset.CompleteUpload(9);
        db.MediaAssets.Add(asset);
        await db.SaveChangesAsync();
        return asset;
    }

    [Fact]
    public void FinalStateCanReplaceImagesAndDeactivatePreviousDefaultTogether()
    {
        var p = new Product(Guid.NewGuid(), "Editor", "editor");
        var option = p.AddOption("COLOR", "Color", 3);
        var red = option.AddValue("RED", "Red");
        var blue = option.AddValue("BLUE", "Blue");
        var first = p.AddVariant("FIRST", 100, null, new Dictionary<Guid, Guid> { [option.Id] = red.Id }, 200);
        var next = p.AddVariant("NEXT", 200, null, new Dictionary<Guid, Guid> { [option.Id] = blue.Id }, 200);
        var oldImage = Guid.NewGuid();
        var image = Guid.NewGuid();
        p.SetMedia([new(p.Id, oldImage, 0, null)]);
        p.EditVariant(first.Id, 100, null, true, oldImage);
        p.ApplyEditorState([new(first.Id, 100, null, false, null), new(next.Id, 200, null, true, image)], [new(p.Id, image, 0, null)], next.Id);
        Assert.False(first.IsDefault);
        Assert.False(first.IsActive);
        Assert.True(next.IsDefault);
        Assert.Equal(image, next.ImageMediaId);
        Assert.Equal(image, p.Media.Single().MediaAssetId);
        Assert.Throws<DomainException>(() => p.ApplyEditorState(
            [new(first.Id, 100, null, true, null), new(next.Id, 200, null, false, image)], [new(p.Id, image, 0, null)], next.Id));
        Assert.True(next.IsDefault);
        Assert.True(next.IsActive);
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task EditorSavesContentOptionsGalleryNewSkuAndDefaultAtomically(string provider)
    {
        await using var f = new FoundationDatabase(provider, true);
        await f.CreateAsync();
        using var scope = f.Scope(Guid.NewGuid());
        var s = Service(scope);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await s.InitializeAsync(default);
        var type = await s.CreateTypeAsync(new("EDITOR", "Editor type", []), default);
        var oldImage = await Asset(db);
        var newImage = await Asset(db);
        var p = await s.CreateAsync(new(type.Id, type.Version, Content("editor") with { Media = [new(oldImage.Id, 0, "Original")] },
            [new("COLOR", "Color", [new("RED", "Red")])], [new("EDITOR-RED", 100, null, new() { ["COLOR"] = "RED" }, ImageMediaId: oldImage.Id)]), default);
        var old = p.Variants.Single();
        var inventory = (await s.InventoryAsync(1, 100, default, p.Id)).Items.Single();
        await s.AdjustAsync(new(inventory.Id, inventory.Version, Guid.NewGuid(), 7, "Existing stock"), default);
        var option = p.Options.Single();
        var changed = Save(p, type) with
        {
            Content = Content("editor") with { Name = "Edited together", Media = [new(newImage.Id, 0, "New image")] },
            OptionEdits = [new(option.Id, "Màu sắc", 0, [new("BLUE", "Blue")], [new(option.Values.Single().Id, "Đỏ", 1)])],
            VariantEdits = [new(old.Id, old.Price, old.CompareAtPrice, false, null)],
            AddedVariants = [new("EDITOR-BLUE", 200, 250, new() { ["COLOR"] = "BLUE" }, ImageMediaId: newImage.Id)],
            DefaultVariantSku = "editor-blue"
        };
        var saved = await s.SaveEditorAsync(changed, default);
        Assert.Equal(p.Id, saved.Id);
        Assert.NotEqual(p.Version, saved.Version);
        Assert.Equal(p.CreatedOn, saved.CreatedOn);
        Assert.True(saved.ModifiedOn >= saved.CreatedOn);
        Assert.Equal(DateTimeKind.Utc, saved.ModifiedOn.Kind);
        Assert.Equal("Edited together", saved.Name);
        Assert.Equal("Màu sắc", saved.Options.Single().Name);
        Assert.Equal(2, saved.Options.Single().Values.Count);
        Assert.False(saved.Variants.Single(v => v.Id == old.Id).IsActive);
        var current = saved.Variants.Single(v => v.IsDefault);
        Assert.Equal("EDITOR-BLUE", current.Sku);
        Assert.Equal(newImage.Id, current.ImageMediaId);
        Assert.Equal(newImage.Id, saved.Media.Single().MediaAssetId);
        Assert.Single(await db.OutboxMessages.ToListAsync());
        var balances = (await s.InventoryAsync(1, 100, default, saved.Id)).Items;
        Assert.Equal(7, balances.Single(i => i.VariantId == old.Id).OnHand);
        Assert.Equal(0, balances.Single(i => i.VariantId == current.Id).OnHand);
        saved = await s.StatusAsync(new(saved.Id, saved.Version, ProductStatus.Published), default);
        saved = await s.SaveEditorAsync(Save(saved, type) with { Content = changed.Content with { Name = "Visible immediately" } }, default);
        Assert.Equal("Visible immediately", (await s.GetBySlugAsync("editor", default)).Name);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task FailedEditorStateAndStaleVersionsRollBackEveryChange(string provider)
    {
        await using var f = new FoundationDatabase(provider, true);
        await f.CreateAsync();
        using var scope = f.Scope();
        var s = Service(scope);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await s.InitializeAsync(default);
        var type = await s.CreateTypeAsync(new("ROLLBACK", "Rollback", []), default);
        var image = await Asset(db);
        var p = await s.CreateAsync(new(type.Id, type.Version, Content("rollback") with { Media = [new(image.Id, 0, null)] },
            [new("COLOR", "Color", [new("RED", "Red")])], [new("ROLLBACK", 100, null, new() { ["COLOR"] = "RED" }, ImageMediaId: image.Id)]), default);
        p = await s.StatusAsync(new(p.Id, p.Version, ProductStatus.Published), default);
        var old = p.Variants.Single();
        var invalid = Save(p, type) with
        {
            Content = Content("rollback") with { Name = "Must roll back" },
            OptionEdits = [new(p.Options.Single().Id, "Changed", 0, [new("BLUE", "Blue")], [])],
            VariantEdits = [new(old.Id, null, null, true, null)]
        };
        await Assert.ThrowsAsync<DomainException>(() => s.SaveEditorAsync(invalid, default));
        var after = await s.GetAsync(p.Id, false, default);
        Assert.Equal(p.Version, after.Version);
        Assert.Equal(p.Name, after.Name);
        Assert.Single(after.Options.Single().Values);
        Assert.Single(after.Media);
        Assert.Empty(await db.OutboxMessages.ToListAsync());
        Assert.Equal(100, after.Variants.Single().Price);
        await Assert.ThrowsAsync<ConflictException>(() => s.SaveEditorAsync(Save(p, type) with { Version = "stale" }, default));
        await Assert.ThrowsAsync<ConflictException>(() => s.SaveEditorAsync(Save(p, type) with { ProductTypeVersion = "stale" }, default));
        await Assert.ThrowsAsync<DomainException>(() => s.SaveEditorAsync(Save(p, type) with
        {
            VariantEdits = [new(Guid.NewGuid(), 100, null, true, null)]
        }, default));
        await Assert.ThrowsAsync<DomainException>(() => s.SaveEditorAsync(Save(p, type) with
        {
            AddedVariants = [new("DUPLICATE-COMBINATION", 100, null, new() { ["COLOR"] = "RED" })]
        }, default));
        Assert.Equal(p.Version, (await s.GetAsync(p.Id, false, default)).Version);
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task EditorPriceAclAndCreateFinalDefaultPreserveLegacyBehavior(string provider)
    {
        await using var f = new FoundationDatabase(provider, true);
        await f.CreateAsync();
        using var scope = f.Scope();
        var s = Service(scope);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await s.InitializeAsync(default);
        var type = await s.CreateTypeAsync(new("CREATE", "Create", []), default);
        var image = await Asset(db);
        var p = await s.CreateAsync(new(type.Id, type.Version, Content("create") with { Media = [new(image.Id, 0, null)] },
            [new("COLOR", "Color", [new("RED", "Red"), new("BLUE", "Blue")])],
            [new("CREATE-RED", 100, null, new() { ["COLOR"] = "RED" }, false), new("CREATE-BLUE", 200, null, new() { ["COLOR"] = "BLUE" }, ImageMediaId: image.Id)],
            DefaultVariantSku: "CREATE-BLUE"), default);
        Assert.Equal("CREATE-BLUE", p.Variants.Single(v => v.IsDefault).Sku);
        Assert.Equal(image.Id, p.Variants.Single(v => v.IsDefault).ImageMediaId);
        var denied = Service(scope, false);
        var blue = p.Variants.Single(v => v.IsDefault);
        p = await denied.SaveEditorAsync(Save(p, type) with
        {
            Content = Content("create") with { Name = "Content is allowed", Media = p.Media.ToList() },
            VariantEdits = [new(blue.Id, blue.Price, blue.CompareAtPrice, true, blue.ImageMediaId)]
        }, default);
        Assert.Equal("Content is allowed", p.Name);
        await Assert.ThrowsAsync<ForbiddenException>(() => denied.SaveEditorAsync(Save(p, type) with
        {
            Content = Content("create") with { Name = "Price bypass" },
            VariantEdits = [new(blue.Id, 300, null, true, null)]
        }, default));
        Assert.Equal(p.Version, (await s.GetAsync(p.Id, false, default)).Version);
        var legacy = await s.CreateAsync(new(type.Id, type.Version, Content("legacy"), [], [new("LEGACY", 10, null, [])]), default);
        Assert.Equal("LEGACY", legacy.Variants.Single(v => v.IsDefault).Sku);
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task BulkStatusIsAtomicAndRejectsStaleOrInvalidSelections(string provider)
    {
        await using var f = new FoundationDatabase(provider, true);
        await f.CreateAsync();
        using var scope = f.Scope();
        var s = Service(scope);
        await s.InitializeAsync(default);
        var type = await s.CreateTypeAsync(new("BULK", "Bulk", []), default);
        var one = await s.CreateAsync(new(type.Id, type.Version, Content("bulk-one"), [], [new("BULK-ONE", 100, null, [])]), default);
        var two = await s.CreateAsync(new(type.Id, type.Version, Content("bulk-two"), [], [new("BULK-TWO", null, null, [])]), default);
        var request = new BulkProductStatus([new(one.Id, one.Version), new(two.Id, two.Version)], ProductStatus.Published);
        await Assert.ThrowsAsync<DomainException>(() => s.BulkStatusAsync(request, default));
        Assert.Equal(one.Version, (await s.GetAsync(one.Id, false, default)).Version);
        Assert.Equal(ProductStatus.Draft, (await s.GetAsync(one.Id, false, default)).Status);
        await Assert.ThrowsAsync<ConflictException>(() => s.BulkStatusAsync(request with { Items = [new(one.Id, "stale"), new(two.Id, two.Version)] }, default));
        two = await s.EditVariantAsync(new(two.Id, two.Variants.Single().Id, two.Version, 200, null, true, null), default);
        var published = await s.BulkStatusAsync(request with { Items = [new(one.Id, one.Version), new(two.Id, two.Version)] }, default);
        Assert.All(published.Items, p => Assert.Equal(ProductStatus.Published, p.Status));
        Assert.Equal(2, (await s.SearchAsync(new(), true, default)).Total);
        await s.BulkStatusAsync(new(published.Items.Select(p => new ProductVersionInput(p.Id, p.Version)).ToList(), ProductStatus.Archived), default);
        Assert.Empty((await s.SearchAsync(new(), true, default)).Items);
        Assert.Equal(2, (await s.InventoryAsync(1, 100, default)).Total);
        await Assert.ThrowsAsync<DomainException>(() => s.BulkStatusAsync(request with { Items = [new(one.Id, one.Version), new(one.Id, one.Version)] }, default));
        await Assert.ThrowsAsync<DomainException>(() => s.BulkStatusAsync(request with { Status = ProductStatus.Draft }, default));
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task SummariesFilterAndSortServerSideWithMetadataAndCategoryCounts(string provider)
    {
        await using var f = new FoundationDatabase(provider, true);
        await f.CreateAsync();
        using var scope = f.Scope();
        var s = Service(scope);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await s.InitializeAsync(default);
        var type = await s.CreateTypeAsync(new("LIST", "Schema display name", []), default);
        var category = await s.CategoryAsync(new(null, null, "Category", "category", null, null, 0, true), default);
        var empty = await s.CategoryAsync(new(null, null, "Empty", "empty", null, null, 0, true), default);
        var gallery = await Asset(db);
        var variantImage = await Asset(db);
        var one = await s.CreateAsync(new(type.Id, type.Version, Content("alpha") with { CategoryIds = [category.Id], Media = [new(gallery.Id, 0, null), new(variantImage.Id, 1, null)] },
            [], [new("ALPHA", 100, null, [], ImageMediaId: variantImage.Id)]), default);
        var two = await s.CreateAsync(new(type.Id, type.Version, Content("beta"), [], [new("BETA", 500, null, [])], ProductFulfillmentKind.DigitalUnique), default);
        var three = await s.CreateAsync(new(type.Id, type.Version, Content("gamma"), [], [new("GAMMA", null, null, [])], ProductFulfillmentKind.ManualExtension), default);
        var stamp = new DateTime(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);
        await db.Products.Where(p => p.Id == one.Id || p.Id == two.Id).ExecuteUpdateAsync(set => set.SetProperty(p => p.LastModifiedOn, stamp));
        await db.Products.Where(p => p.Id == three.Id).ExecuteUpdateAsync(set => set.SetProperty(p => p.LastModifiedOn, stamp.AddDays(-1)));
        var byModified = await s.SearchAsync(new(), false, default);
        Assert.Equal(new[] { one.Id, two.Id }.Order().Append(three.Id), byModified.Items.Select(p => p.Id));
        var summary = byModified.Items.Single(p => p.Id == one.Id);
        Assert.Equal(type.Id, summary.ProductTypeId);
        Assert.Equal(type.Name, summary.ProductTypeName);
        Assert.Equal(variantImage.Id, summary.ThumbnailMediaId);
        Assert.Equal(one.Version, summary.Version);
        Assert.Equal(stamp, summary.ModifiedOn);
        Assert.Equal(new[] { three.Id, two.Id, one.Id }, (await s.SearchAsync(new() { SortBy = "name", SortDirection = "desc" }, false, default)).Items.Select(p => p.Id));
        Assert.Equal(new[] { one.Id, two.Id, three.Id }, (await s.SearchAsync(new() { SortBy = "price", SortDirection = "asc" }, false, default)).Items.Select(p => p.Id));
        Assert.Equal(new[] { two.Id, one.Id, three.Id }, (await s.SearchAsync(new() { SortBy = "price", SortDirection = "desc" }, false, default)).Items.Select(p => p.Id));
        var filtered = await s.SearchAsync(new() { FulfillmentKind = ProductFulfillmentKind.DigitalUnique, PageSize = 1 }, false, default);
        Assert.Equal(1, filtered.Total);
        Assert.Equal(two.Id, filtered.Items.Single().Id);
        var counts = await s.CategorySummariesAsync(default);
        Assert.Equal(1, counts.Single(c => c.Id == category.Id).ProductCount);
        Assert.Equal(0, counts.Single(c => c.Id == empty.Id).ProductCount);
        await Assert.ThrowsAsync<DomainException>(() => s.SearchAsync(new() { SortBy = "unknown" }, false, default));
        await Assert.ThrowsAsync<DomainException>(() => s.SearchAsync(new() { FulfillmentKind = (ProductFulfillmentKind)99 }, false, default));
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task HttpEditorBulkAndCreateOnlyMediaPermissionsAreEnforced(string provider)
    {
        await using var f = new FoundationDatabase(provider, true);
        await f.CreateAsync();
        var previousEmail = Environment.GetEnvironmentVariable("BOILERATE_BOOTSTRAP_EMAIL");
        var previousPassword = Environment.GetEnvironmentVariable("BOILERATE_BOOTSTRAP_PASSWORD");
        try
        {
            Environment.SetEnvironmentVariable("BOILERATE_BOOTSTRAP_EMAIL", "editor@example.test");
            Environment.SetEnvironmentVariable("BOILERATE_BOOTSTRAP_PASSWORD", "CatalogEditor!29874");
            using var bootstrap = f.Scope();
            await bootstrap.ServiceProvider.GetRequiredService<FoundationBootstrap>().RunAsync(["--bootstrap-admin"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable("BOILERATE_BOOTSTRAP_EMAIL", previousEmail);
            Environment.SetEnvironmentVariable("BOILERATE_BOOTSTRAP_PASSWORD", previousPassword);
        }
        await using var app = await TestHostApplication.StartAsync(f, true);
        var client = app.GetTestClient();
        using (var anonymous = ImageForm())
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/media", anonymous)).StatusCode);
        var login = await client.PostAsJsonAsync("/api/tokens/get", new TokenRequest("editor@example.test", "CatalogEditor!29874"));
        login.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<TokenResponse>())!.Token);
        using var scope = f.Scope();
        var s = Service(scope);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await s.InitializeAsync(default);
        var type = await s.CreateTypeAsync(new("HTTP-EDITOR", "HTTP Editor", []), default);
        var p = await s.CreateAsync(new(type.Id, type.Version, Content("http-editor"), [], [new("HTTP-EDITOR", 100, null, [])]), default);
        var response = await client.PutAsJsonAsync($"/api/products/{p.Id}/editor", Save(p, type) with { Content = Content("http-editor") with { Name = "HTTP save" } });
        response.EnsureSuccessStatusCode();
        p = (await response.Content.ReadFromJsonAsync<ProductDto>())!;
        Assert.Equal("HTTP save", p.Name);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/products/{Guid.NewGuid()}/editor", Save(p, type))).StatusCode);
        (await client.GetAsync("/api/categories/summaries")).EnsureSuccessStatusCode();
        var bulk = await client.PostAsJsonAsync("/api/products/bulk-status", new BulkProductStatus([new(p.Id, p.Version)], ProductStatus.Published));
        bulk.EnsureSuccessStatusCode();
        Assert.Equal(ProductStatus.Published, (await bulk.Content.ReadFromJsonAsync<BulkProductStatusResult>())!.Items.Single().Status);
        var productFunction = await db.Functions.SingleAsync(x => x.Code == "Product");
        var update = await db.Actions.SingleAsync(x => x.Code == "Update");
        await db.Permissions.Where(g => g.FunctionId == productFunction.Id && g.ActionId == update.Id).ExecuteUpdateAsync(set => set.SetProperty(g => g.DeletedOn, DateTime.UtcNow));
        using (var createOnly = ImageForm())
            (await client.PostAsync("/api/media", createOnly)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"/api/products/{p.Id}/editor", Save(p, type))).StatusCode);
        var create = await db.Actions.SingleAsync(x => x.Code == "Create");
        var publish = await db.Actions.SingleAsync(x => x.Code == "Publish");
        await db.Permissions.Where(g => g.FunctionId == productFunction.Id && (g.ActionId == create.Id || g.ActionId == publish.Id))
            .ExecuteUpdateAsync(set => set.SetProperty(g => g.DeletedOn, DateTime.UtcNow));
        var assetsBefore = await db.MediaAssets.CountAsync();
        using (var denied = ImageForm())
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/media", denied)).StatusCode);
        Assert.Equal(assetsBefore, await db.MediaAssets.CountAsync());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/products/bulk-status", new BulkProductStatus([new(p.Id, p.Version)], ProductStatus.Archived))).StatusCode);
    }

    private static MultipartFormDataContent ImageForm()
    {
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent([137, 80, 78, 71, 13, 10, 26, 10, 1]), "file", "catalog-editor.png");
        return form;
    }
}
