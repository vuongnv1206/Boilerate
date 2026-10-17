using System.Net;
using System.Net.Http.Json;
using Boilerate.Application.Catalog;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Identity.Tokens;
using Boilerate.Application.Identity.Users;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using Boilerate.Domain.Inventory;
using Boilerate.Domain.Media;
using Boilerate.Domain.Messaging;
using Boilerate.Infrastructure.Commerce;
using Boilerate.Infrastructure.FileStorage;
using Boilerate.Infrastructure.Persistence.Context;
using Boilerate.Infrastructure.Persistence.Initialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;
namespace Boilerate.Tests;

[Collection("Foundation")]
public sealed class CommerceIntegrationTests
{
    private static CommerceService Service(IServiceScope scope, bool price = true)
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = Substitute.For<IUserService>();
        users.HasPermissionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(price);
        return new(db, new CommerceUnitOfWork(db), scope.ServiceProvider.GetRequiredService<ICurrentUser>(), users, Options.Create(new CommerceSettings()),
            scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>());
    }
    private static ProductContent Content(string slug, params Guid[] categories) => new("Product " + slug, slug, null, categories.ToList(), [], []);
    private static CreateProduct Simple(ProductType type, string slug, decimal? price = 100, params Guid[] categories) =>
        new(type.Id, type.Version, Content(slug, categories), [], [new(slug, price, null, new())]);
    private static OutboxProcessor Processor(IServiceScope scope, int attempts = 8)
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return new(db, new CommerceUnitOfWork(db), scope.ServiceProvider.GetRequiredService<LocalFileStorageService>(), Options.Create(new CommerceSettings { MaximumAttempts = attempts }), NullLogger<OutboxProcessor>.Instance);
    }
    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task CatalogRelationsTypedFiltersPublicationAndVersionsWork(string provider)
    {
        await using var f = new FoundationDatabase(provider, true);
        await f.CreateAsync();
        using var scope = f.Scope(Guid.NewGuid());
        var service = Service(scope);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await service.InitializeAsync(default);
        var type = await service.CreateTypeAsync(new("SHIRT", "Shirt", [
            new("material",ProductFieldType.Choice,"Material",null,true,true,[new("cotton","Cotton")]),
            new("weight",ProductFieldType.Number,"Weight",null,false,true,[]), new("release",ProductFieldType.Date,"Release",null,false,true,[]), new("finish",ProductFieldType.Text,"Finish",null,false,true,[]), new("organic",ProductFieldType.Boolean,"Organic",null,false,true,[])]), default);
        var a = await service.CategoryAsync(new(null, null, "A", "a", null, null, 0, true), default);
        var b = await service.CategoryAsync(new(null, null, "B", "b", null, a.Id, 0, true), default);
        await Assert.ThrowsAsync<DomainException>(() => service.CategoryAsync(new(a.Id, a.Version, "A", "a", null, b.Id, 0, true), default));
        var p = await service.CreateAsync(Simple(type, "shirt", 100, a.Id, b.Id), default);
        Assert.Equal(2, p.CategoryIds.Count);
        Assert.Single(await db.Set<ProductCategory>().Where(c => c.CategoryId == a.Id).ToListAsync());
        await Assert.ThrowsAsync<ConflictException>(() => service.DeleteCategoryAsync(new(a.Id, a.Version), default));
        await Assert.ThrowsAsync<DomainException>(() => service.StatusAsync(new(p.Id, p.Version, ProductStatus.Published), default));
        Assert.Empty((await service.SearchAsync(new(), true, default)).Items);
        var material = type.Fields.Single(x => x.DataType == ProductFieldType.Choice);
        var weight = type.Fields.Single(x => x.DataType == ProductFieldType.Number);
        var release = type.Fields.Single(x => x.DataType == ProductFieldType.Date);
        var content = Content("shirt", a.Id, b.Id) with
        {
            Fields = [new(material.Id, material.DataType, ChoiceId: material.Choices.Single().Id), new(weight.Id, weight.DataType, NumberValue: 220.5m), new(release.Id, release.DataType, DateValue: new DateOnly(2026, 10, 16))]
        };
        var finish = type.Fields.Single(f => f.DataType == ProductFieldType.Text); var organic = type.Fields.Single(f => f.DataType == ProductFieldType.Boolean);
        content.Fields.Add(new(finish.Id,finish.DataType,TextValue:"matte ")); content.Fields.Add(new(organic.Id,organic.DataType,BooleanValue:false));
        p = await service.EditAsync(new(p.Id, p.Version, type.Version, content), default);
        var stale = p.Version;
        p = await service.StatusAsync(new(p.Id, p.Version, ProductStatus.Published), default);
        await Assert.ThrowsAsync<ConflictException>(() => service.EditAsync(new(p.Id, stale, type.Version, content), default));
        var result = await service.SearchAsync(new()
        {
            Fields = [new(weight.Id, "range", NumberValue: 220, NumberTo: 221), new(release.Id, "eq", DateValue: new DateOnly(2026, 10, 16)), new(material.Id, "in", ChoiceIds: [material.Choices.Single().Id])]
        }, true, default);
        Assert.Equal(1, result.Total);
        Assert.Equal(p.Id, Assert.Single(result.Items).Id);
        foreach (var invalid in new[] { decimal.MaxValue, decimal.MinValue, 100000000000000m, -100000000000000m, 220.50001m })
        {
            await Assert.ThrowsAsync<DomainException>(() => service.SearchAsync(new() { Fields = [new(weight.Id,"eq",NumberValue:invalid)] },true,default));
            await Assert.ThrowsAsync<DomainException>(() => service.SearchAsync(new() { Fields = [new(weight.Id,"range",NumberValue:0,NumberTo:invalid)] },true,default));
        }
        foreach (var invalid in new[] { decimal.MaxValue, decimal.MinValue, 10000000000000000m, 100.5m })
        {
            await Assert.ThrowsAsync<DomainException>(() => service.SearchAsync(new() { MinimumPrice=invalid },true,default));
            await Assert.ThrowsAsync<DomainException>(() => service.SearchAsync(new() { MaximumPrice=invalid },true,default));
        }
        Assert.Equal(0,(await service.SearchAsync(new() { Fields=[new(weight.Id,"eq",NumberValue:99999999999999.9999m)] },true,default)).Total);
        Assert.Equal(1,(await service.SearchAsync(new() { MinimumPrice=0,MaximumPrice=9999999999999999m },true,default)).Total);
        Assert.Equal(1,(await service.SearchAsync(new() { Fields = [new(finish.Id,"eq",TextValue:"matte "),new(organic.Id,"eq",BooleanValue:false)] },true,default)).Total);
        Assert.Equal(0,(await service.SearchAsync(new() { Fields = [new(finish.Id,"eq",TextValue:"matte")] },true,default)).Total);
        Assert.Equal(0,(await service.SearchAsync(new() { Fields = [new(finish.Id,"eq",TextValue:"MATTE ")] },true,default)).Total);
        await Assert.ThrowsAsync<DomainException>(() => service.SearchAsync(new() { Fields = [new(weight.Id, "contains", TextValue: "220")] }, true, default));
        p = await service.EditAsync(new(p.Id, p.Version, type.Version, content with
        {
            Name = "Updated immediately",
            CategoryIds = [b.Id]
        }), default);
        Assert.Equal("Updated immediately", (await service.GetBySlugAsync("shirt", default)).Name);
        Assert.Single(p.CategoryIds);
        await Assert.ThrowsAsync<DomainException>(() => service.EditVariantAsync(new(p.Id, p.Variants.Single().Id, p.Version, null, null, true, null), default));
        p = await service.GetAsync(p.Id, false, default);
        Assert.Equal(100, p.Variants.Single().Price);
        var denied = Service(scope, false);
        await Assert.ThrowsAsync<ForbiddenException>(() => denied.EditVariantAsync(new(p.Id, p.Variants.Single().Id, p.Version, 200, null, true, null), default));
        p = await service.StatusAsync(new(p.Id, p.Version, ProductStatus.Archived), default);
        Assert.Empty((await service.SearchAsync(new(), true, default)).Items);
        Assert.Single((await service.InventoryAsync(1, 20, default)).Items);
        Assert.False(db.Database.HasPendingModelChanges());
        type = await service.EditTypeAsync(new(type.Id,type.Version,type.Name,true,[],[
            new(material.Id,material.Label,null,true,false,0,[],[])]),default);
        var without = await service.CreateAsync(Simple(type,"without-material"),default);
        await service.StatusAsync(new(without.Id,without.Version,ProductStatus.Published),default);
        await Assert.ThrowsAsync<ConflictException>(() => service.EditTypeAsync(new(type.Id,type.Version,type.Name,true,[],[
            new(material.Id,material.Label,null,true,true,0,[],[])]),default));
    }
    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task DatabaseRejectsDuplicateAndCrossProductRelations(string provider)
    {
        await using var f = new FoundationDatabase(provider, true);
        await f.CreateAsync();
        using var scope = f.Scope();
        var s = Service(scope);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await s.InitializeAsync(default);
        var type = await s.CreateTypeAsync(new("A", "A", [new("text", ProductFieldType.Text, "Text", null, false, true, [])]), default);
        var category = await s.CategoryAsync(new(null, null, "A", "a", null, null, 0, true), default);
        List<OptionInput> options = [new("color", "Color", [new("black", "Black"), new("white", "White")])];
        var p = await s.CreateAsync(new(type.Id, type.Version, Content("first", category.Id), options, [new("FIRST-B", 100, null, new() { ["color"] = "black" }), new("FIRST-W", 200, null, new() { ["color"] = "white" })]), default);
        var second = await s.CreateAsync(new(type.Id, type.Version, Content("second"), options, [new("SECOND-B", 100, null, new() { ["color"] = "black" })]), default);
        async Task Reject(FormattableString sql)
        {
            var error = await Record.ExceptionAsync(() => db.Database.ExecuteSqlInterpolatedAsync(sql));
            Assert.True(error is Microsoft.Data.SqlClient.SqlException { Number: 547 or 2601 or 2627 } || error is Npgsql.PostgresException { SqlState: "23503" or "23505" or "23514" }, error?.ToString() ?? "Constraint was not enforced");
        }
        await Reject($"INSERT INTO \"Catalog\".\"ProductCategories\" (\"ProductId\",\"CategoryId\") VALUES ({p.Id},{category.Id})");
        var foreign = second.Options.Single();
        var variant = p.Variants.Single(v => v.IsDefault);
        var alternate = p.Variants.Single(v => !v.IsDefault).Id;
        await Reject($"UPDATE \"Catalog\".\"VariantOptionValues\" SET \"OptionValueId\"={foreign.Values.First().Id} WHERE \"VariantId\"={variant.Id}");
        await Reject($"UPDATE \"Catalog\".\"VariantOptionValues\" SET \"OptionId\"={foreign.Id},\"OptionValueId\"={foreign.Values.First().Id} WHERE \"VariantId\"={variant.Id}");
        await Reject($"UPDATE \"Catalog\".\"ProductVariants\" SET \"IsDefault\"={true} WHERE \"Id\"={alternate}");
        await Reject($"UPDATE \"Catalog\".\"ProductVariants\" SET \"NormalizedSku\"={variant.Sku.ToUpperInvariant()} WHERE \"Id\"={second.Variants.Single().Id}");
        var defaultId = alternate;
        p = await s.DefaultAsync(new(p.Id, defaultId, p.Version), default);
        Assert.Equal(defaultId, p.Variants.Single(v => v.IsDefault).Id);
        p = await s.EditVariantAsync(new(p.Id, variant.Id, p.Version, 100, null, false, null), default);
        Assert.False(p.Variants.Single(v => v.Id == variant.Id).IsActive);
        var otherType = await s.CreateTypeAsync(new("OTHER", "Other", [new("text", ProductFieldType.Text, "Text", null, false, true, [])]), default);
        var wrongField = otherType.Fields.Single();
        await Reject($"INSERT INTO \"Catalog\".\"ProductFieldValues\" (\"ProductId\",\"ProductTypeId\",\"FieldDefinitionId\",\"FieldType\",\"TextValue\") VALUES ({p.Id},{type.Id},{wrongField.Id},{1},{"wrong"})");
        var field = type.Fields.Single();
        await Reject($"INSERT INTO \"Catalog\".\"ProductFieldValues\" (\"ProductId\",\"ProductTypeId\",\"FieldDefinitionId\",\"FieldType\",\"TextValue\",\"NumberValue\") VALUES ({p.Id},{type.Id},{field.Id},{1},{"mixed"},{1})");
        await Reject($"DELETE FROM \"Catalog\".\"Categories\" WHERE \"Id\"={category.Id}");
        Assert.Equal(2, await db.Products.CountAsync());
    }
    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task InventoryIsAtomicIdempotentConcurrentAndAppendOnly(string provider)
    {
        await using var f = new FoundationDatabase(provider, true);
        await f.CreateAsync();
        InventoryDto item;
        using (var scope = f.Scope())
        {
            var s = Service(scope);
            await s.InitializeAsync(default);
            var type = await s.CreateTypeAsync(new("A", "A", []), default);
            await s.CreateAsync(Simple(type, "stock"), default);
            item = Assert.Single((await s.InventoryAsync(1, 20, default)).Items);
            var operation = Guid.NewGuid();
            var first = await s.AdjustAsync(new(item.Id, item.Version, operation, 1, "Initial"), default);
            Assert.Equal(first, await s.AdjustAsync(new(item.Id, item.Version, operation, 1, "Initial"), default));
            await Assert.ThrowsAsync<ConflictException>(() => s.AdjustAsync(new(item.Id, item.Version, operation, 2, "Different"), default));
            item = Assert.Single((await s.InventoryAsync(1, 20, default)).Items);
        }
        async Task<bool> Compete()
        {
            using var scope = f.Scope();
            try
            {
                await Service(scope).AdjustAsync(new(item.Id, item.Version, Guid.NewGuid(), -1, "Sale simulation"), default);
                return true;
            }
            catch (ConflictException) { return false; }
        }
        var winners = await Task.WhenAll(Compete(), Compete());
        Assert.Single(winners.Where(x => x));
        using (var scope = f.Scope())
        {
            var s = Service(scope);
            var current = Assert.Single((await s.InventoryAsync(1, 20, default)).Items);
            Assert.Equal(0, current.OnHand);
            await Assert.ThrowsAsync<DomainException>(() => s.AdjustAsync(new(current.Id, current.Version, Guid.NewGuid(), -1, "Too much"), default));
            Assert.Equal(2, (await s.MovementsAsync(item.Id, 1, 20, default)).Total);
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.StockMovements.Remove(await db.StockMovements.FirstAsync());
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        }
    }
    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task MediaCleanupSurvivesRetryRollbackAndProtectsAttachedFiles(string provider)
    {
        await using var f = new FoundationDatabase(provider, true);
        await f.CreateAsync();
        Guid assetId;
        ProductDto p;
        ProductType type;
        using (var scope = f.Scope())
        {
            var s = Service(scope);
            await s.InitializeAsync(default);
            type = await s.CreateTypeAsync(new("A", "A", []), default);
            using var stream = new MemoryStream(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 1 });
            var asset = await scope.ServiceProvider.GetRequiredService<ICommerceMediaService>().UploadAsync(new FormFile(stream, 0, stream.Length, "file", "image.png"), default);
            assetId = asset.Id;
            p = await s.CreateAsync(Simple(type, "media") with
            {
                Content = Content("media") with
                {
                    Media = [new(assetId, 0, "Test")]
                }
            }, default);
            await Assert.ThrowsAsync<NotFoundException>(() => scope.ServiceProvider.GetRequiredService<ICommerceMediaService>().OpenAsync(assetId, true, default));
            p = await s.StatusAsync(new(p.Id, p.Version, ProductStatus.Published), default);
            var opened = await scope.ServiceProvider.GetRequiredService<ICommerceMediaService>().OpenAsync(assetId, true, default);
            opened.Stream.Dispose();
            await Processor(scope).CleanupAsync(assetId, default);
            opened = await scope.ServiceProvider.GetRequiredService<ICommerceMediaService>().OpenAsync(assetId, true, default);
            opened.Stream.Dispose();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await Assert.ThrowsAsync<DomainException>(() => s.EditAsync(new(p.Id, p.Version, type.Version, Content("media") with { Fields = [new(Guid.NewGuid(), ProductFieldType.Text, TextValue: "bad")] }), default));
            Assert.Equal(0, await db.OutboxMessages.CountAsync());
            Assert.Single((await s.GetAsync(p.Id, false, default)).Media);
            p = await s.EditAsync(new(p.Id, p.Version, type.Version, Content("media")), default);
            Assert.Equal(1, await db.OutboxMessages.CountAsync());
        }
        // A different scope represents restart after the product transaction committed.
        using (var scope = f.Scope())
        {
            Assert.True(await Processor(scope).RunOneAsync(default));
            Assert.False(await Processor(scope).RunOneAsync(default));
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(MediaState.Deleted, (await db.MediaAssets.AsNoTracking().SingleAsync(a => a.Id == assetId)).State);
            Assert.Equal(OutboxState.Completed, (await db.OutboxMessages.SingleAsync()).State);
            await Processor(scope).CleanupAsync(assetId, default); // Duplicate after file delete is harmless.
            await Assert.ThrowsAsync<DomainException>(() => Service(scope).EditAsync(new(p.Id, p.Version, type.Version, Content("media") with { Media = [new(assetId, 0, null)] }), default));
        }
    }
    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task OutboxTwoWorkersExpiredLeaseDeadLetterAndReplayAreSafe(string provider)
    {
        await using var f = new FoundationDatabase(provider, true);
        await f.CreateAsync();
        Guid messageId;
        using (var scope = f.Scope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var message = OutboxMessage.Cleanup(Guid.NewGuid(), Guid.Empty);
            message.State = OutboxState.Processing;
            message.LeaseUntil = DateTime.UtcNow.AddMinutes(-1);
            message.LeaseToken = Guid.NewGuid();
            db.OutboxMessages.Add(message);
            await db.SaveChangesAsync();
            messageId = message.MessageId;
        }
        async Task Work()
        {
            using var scope = f.Scope();
            await Processor(scope).RunOneAsync(default);
        }
        await Task.WhenAll(Work(), Work());
        using (var scope = f.Scope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var message = await db.OutboxMessages.SingleAsync();
            Assert.Equal(OutboxState.Completed, message.State);
            Assert.Equal(1, message.Attempts);
            await db.OutboxMessages.Where(m => m.MessageId == messageId).ExecuteUpdateAsync(s => s.SetProperty(m => m.Payload, "invalid").SetProperty(m => m.State, OutboxState.Pending));
            Assert.True(await Processor(scope, 1).RunOneAsync(default));
            Assert.Equal(OutboxState.DeadLetter, (await db.OutboxMessages.AsNoTracking().SingleAsync()).State);
            Assert.True(await Service(scope).ReplayAsync(new(messageId), default));
            Assert.Equal(OutboxState.Pending, (await db.OutboxMessages.AsNoTracking().SingleAsync()).State);
            Assert.True(await db.Trails.AnyAsync(t => t.TableName == "OutboxMessages"));
        }
    }
    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task ExpiredUploadsAndConcurrentAttachCleanupDoNotLeaveBrokenReferences(string provider)
    {
        await using var f = new FoundationDatabase(provider, true);
        await f.CreateAsync();
        Guid assetId;
        ProductDto p;
        ProductType type;
        using (var scope = f.Scope())
        {
            var s = Service(scope);
            await s.InitializeAsync(default);
            type = await s.CreateTypeAsync(new("RACE", "Race", []), default);
            p = await s.CreateAsync(Simple(type, "race"), default);
            using var input = new MemoryStream(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 1 });
            var uploaded = await scope.ServiceProvider.GetRequiredService<ICommerceMediaService>().UploadAsync(new FormFile(input, 0, input.Length, "file", "race.png"), default);
            assetId = uploaded.Id;
            // Upload validation fails after durable intent; expired intent is swept after restart.
            using var bad = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
            await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => scope.ServiceProvider.GetRequiredService<ICommerceMediaService>().UploadAsync(new FormFile(bad, 0, bad.Length, "file", "bad.png"), default));
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.MediaAssets.Where(a => a.State == MediaState.Uploading).ExecuteUpdateAsync(s => s.SetProperty(a => a.ExpiresAt, DateTime.UtcNow.AddDays(-1)));
            // A page full of older dead letters must not hide the failed upload from the sweeper.
            for (int i = 0; i < 101; i++)
            {
                var blocked = new MediaAsset(".png"); db.MediaAssets.Add(blocked);
                db.Entry(blocked).Property(a => a.ExpiresAt).CurrentValue = DateTime.UtcNow.AddDays(-2);
                var poison = OutboxMessage.Cleanup(blocked.Id,Guid.Empty); poison.State = OutboxState.DeadLetter; db.OutboxMessages.Add(poison);
            }
            await db.SaveChangesAsync();
            await Processor(scope).SweepAsync(default);
            Assert.True(await Processor(scope).RunOneAsync(default));
            Assert.Equal(1, await db.MediaAssets.CountAsync(a => a.State == MediaState.Deleted));
        }
        async Task<bool> Attach()
        {
            using var scope = f.Scope();
            try
            {
                await Service(scope).EditAsync(new(p.Id, p.Version, type.Version, Content("race") with
                {
                    Media = [new(assetId, 0, null)]
                }), default);
                return true;
            }
            catch (DomainException) { return false; }
        }
        async Task Clean()
        {
            using var scope = f.Scope();
            await Processor(scope).CleanupAsync(assetId, default);
        }
        var attach = Attach();
        await Task.WhenAll(attach, Clean());
        using (var scope = f.Scope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            bool linked = await db.Set<ProductMedia>().AnyAsync(m => m.MediaAssetId == assetId);
            var asset = await db.MediaAssets.AsNoTracking().SingleAsync(a => a.Id == assetId);
            Assert.Equal(await attach, linked);
            Assert.Equal(linked ? MediaState.Ready : MediaState.Deleted, asset.State);
            if (linked)
            {
                var current = await Service(scope).GetAsync(p.Id, false, default);
                await Service(scope).StatusAsync(new(p.Id, current.Version, ProductStatus.Archived), default);
                await Processor(scope).CleanupAsync(assetId, default);
                Assert.Equal(MediaState.Ready, (await db.MediaAssets.AsNoTracking().SingleAsync(a => a.Id == assetId)).State);
            }
        }
    }
    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task HttpPermissionsCannotBeBypassedThroughNestedSkuPrices(string provider)
    {
        await using var f = new FoundationDatabase(provider, true);
        await f.CreateAsync();
        var oldEmail = Environment.GetEnvironmentVariable("BOILERATE_BOOTSTRAP_EMAIL");
        var oldPassword = Environment.GetEnvironmentVariable("BOILERATE_BOOTSTRAP_PASSWORD");
        try
        {
            Environment.SetEnvironmentVariable("BOILERATE_BOOTSTRAP_EMAIL", "commerce@example.test");
            Environment.SetEnvironmentVariable("BOILERATE_BOOTSTRAP_PASSWORD", "CommercePassword!29874");
            using var scope = f.Scope();
            await scope.ServiceProvider.GetRequiredService<FoundationBootstrap>().RunAsync(["--bootstrap-admin"]);
        }
        finally { Environment.SetEnvironmentVariable("BOILERATE_BOOTSTRAP_EMAIL", oldEmail); Environment.SetEnvironmentVariable("BOILERATE_BOOTSTRAP_PASSWORD", oldPassword); }
        await using var app = await TestHostApplication.StartAsync(f, true);
        var client = app.GetTestClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/products/variants/preview", new PreviewVariants([]))).StatusCode);
        var login = await client.PostAsJsonAsync("/api/tokens/get", new TokenRequest("commerce@example.test", "CommercePassword!29874"));
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<TokenResponse>())!;
        client.DefaultRequestHeaders.Authorization = new("Bearer", token.Token);
        (await client.PostAsync("/api/inventory/initialize", null)).EnsureSuccessStatusCode();
        var createdType = await client.PostAsJsonAsync("/api/product-types", new CreateProductType("HTTP", "HTTP", []));
        createdType.EnsureSuccessStatusCode();
        var type = await createdType.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var typeId = type.GetProperty("id").GetGuid();
        var typeVersion = type.GetProperty("version").GetString()!;
        var request = new CreateProduct(typeId, typeVersion, Content("http"), [], [new("HTTP", 100, null, new())]);
        var response = await client.PostAsJsonAsync("/api/products", request);
        response.EnsureSuccessStatusCode();
        var product = (await response.Content.ReadFromJsonAsync<ProductDto>())!;
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/catalog/products/http")).StatusCode);
        using (var scope = f.Scope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var price = await db.Functions.SingleAsync(x => x.Code == "Price");
            await db.Permissions.Where(p => p.FunctionId == price.Id).ExecuteUpdateAsync(s => s.SetProperty(p => p.DeletedOn, DateTime.UtcNow));
        }
        response = await client.PutAsJsonAsync($"/api/products/{product.Id}/variants/{product.Variants.Single().Id}", new EditVariant(product.Id, product.Variants.Single().Id, product.Version, 200, null, true, null));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        response = await client.PostAsJsonAsync("/api/products", request with
        {
            Content = Content("second-http"),
            Variants = [new("HTTP-SECOND", 200, null, new())]
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        response = await client.PutAsJsonAsync($"/api/products/{product.Id}", new EditProduct(product.Id, product.Version, typeVersion, Content("http") with
        {
            Name = "Content still editable"
        }));
        response.EnsureSuccessStatusCode();
        product = (await response.Content.ReadFromJsonAsync<ProductDto>())!;
        response = await client.PostAsJsonAsync($"/api/products/{product.Id}/status", new ChangeProductStatus(product.Id, product.Version, ProductStatus.Published));
        response.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/catalog/products/http")).StatusCode);
    }
}
