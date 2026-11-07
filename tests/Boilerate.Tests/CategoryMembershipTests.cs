using System.Net;
using System.Net.Http.Json;
using Boilerate.Application.Catalog.Categories;
using Boilerate.Application.Catalog.Products;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Identity.Tokens;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using Boilerate.Infrastructure.Persistence.Context;
using Boilerate.Infrastructure.Persistence.Initialization;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Boilerate.Tests;

[Collection("Foundation")]
public sealed class CategoryMembershipTests
{
    private static CatalogTestClient Service(IServiceScope scope) => CatalogTestClient.ForScope(scope);

    private static Task<ProductDto> Product(CatalogTestClient service, ProductType type, string slug, params Guid[] categories) =>
        service.CreateAsync(new(type.Id, type.Version, new(slug, slug, null, categories.ToList(), [], []), [],
            [new(slug, 100, null, [])]), default);

    private static Task<Category> Category(CatalogTestClient service, string slug) =>
        service.CategoryAsync(new(null, null, slug, slug, null, null, 0, true), default);

    [Fact]
    public async Task ValidatorReturnsValidationErrorsForNullBatchEntries()
    {
        var result = await new SetCategoryProductsRequestValidator().ValidateAsync(new SetCategoryProductsRequest(Guid.NewGuid(), [null!]));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Changes[0]");
    }

    [Fact]
    public void MembershipCannotExceedOneHundredCategoriesAndCanRemoveAtTheLimit()
    {
        var product = new Product(Guid.NewGuid(), "Membership", "membership-limit");
        var categories = Enumerable.Range(0, 100).Select(_ => Guid.NewGuid()).ToArray();
        product.SetCategories(categories);
        var version = product.Version;
        var next = Guid.NewGuid();
        Assert.Throws<DomainException>(() => product.SetCategoryMembership(next, true));
        Assert.Equal(version, product.Version);
        Assert.Equal(100, product.Categories.Count);
        product.SetCategoryMembership(categories[0], false);
        product.SetCategoryMembership(next, true);
        Assert.Equal(100, product.Categories.Count);
        Assert.DoesNotContain(product.Categories, membership => membership.CategoryId == categories[0]);
        Assert.Contains(product.Categories, membership => membership.CategoryId == next);
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task MembershipDeltaPreservesOtherCategoriesAndDoesNotTouchNoOps(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var service = Service(scope);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await service.InitializeAsync(default);
        var type = await service.CreateTypeAsync(new("MEMBERSHIP", "Membership", []), default);
        var target = await Category(service, "target");
        var retained = await Category(service, "retained");
        var first = await Product(service, type, "first", retained.Id);
        var second = await Product(service, type, "second", target.Id, retained.Id);
        var beforeAudit = await db.Trails.CountAsync();

        var changed = await service.SetCategoryProductsAsync(new(target.Id,
            [new(first.Id, first.Version, true), new(second.Id, second.Version, false)]), default);
        var updatedFirst = await service.GetAsync(first.Id, false, default);
        var updatedSecond = await service.GetAsync(second.Id, false, default);
        Assert.Equal(new[] { target.Id, retained.Id }.Order(), updatedFirst.CategoryIds.Order());
        Assert.Equal(new[] { retained.Id }, updatedSecond.CategoryIds);
        Assert.NotEqual(first.Version, updatedFirst.Version);
        Assert.NotEqual(second.Version, updatedSecond.Version);
        Assert.Equal(updatedFirst.Version, changed.Single(result => result.ProductId == first.Id).Version);
        Assert.Equal(updatedSecond.Version, changed.Single(result => result.ProductId == second.Id).Version);
        Assert.True(await db.Trails.CountAsync() > beforeAudit);

        var noOpAudit = await db.Trails.CountAsync();
        var unchanged = await service.SetCategoryProductsAsync(new(target.Id,
            [new(first.Id, updatedFirst.Version, true), new(second.Id, updatedSecond.Version, false)]), default);
        Assert.Equal(updatedFirst.Version, unchanged.Single(result => result.ProductId == first.Id).Version);
        Assert.Equal(updatedSecond.Version, unchanged.Single(result => result.ProductId == second.Id).Version);
        Assert.Equal(noOpAudit, await db.Trails.CountAsync());
        Assert.Empty(await service.SetCategoryProductsAsync(new(target.Id, []), default));
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task StaleVersionRollsBackEveryMembershipAndAuditInTheBatch(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var service = Service(scope);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await service.InitializeAsync(default);
        var type = await service.CreateTypeAsync(new("ROLLBACK", "Rollback", []), default);
        var target = await Category(service, "target");
        var retained = await Category(service, "retained");
        var products = new[] { await Product(service, type, "first", retained.Id), await Product(service, type, "second", retained.Id) }
            .OrderBy(product => product.Id).ToArray();
        var current = await service.EditAsync(new(products[1].Id, products[1].Version, type.Version,
            new("Changed separately", products[1].Slug, null, [retained.Id], [], [])), default);
        var audit = await db.Trails.CountAsync();

        await Assert.ThrowsAsync<ConflictException>(() => service.SetCategoryProductsAsync(new(target.Id,
            products.Select(product => new CategoryProductChange(product.Id, product.Version, true)).ToList()), default));

        var first = await service.GetAsync(products[0].Id, false, default);
        var second = await service.GetAsync(products[1].Id, false, default);
        Assert.Equal(products[0].Version, first.Version);
        Assert.Equal(current.Version, second.Version);
        Assert.Equal(new[] { retained.Id }, first.CategoryIds);
        Assert.Equal(new[] { retained.Id }, second.CategoryIds);
        Assert.Equal(audit, await db.Trails.CountAsync());
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task MissingDeletedAndOverLimitSelectionsLeaveExistingMembershipsUntouched(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var service = Service(scope);
        await service.InitializeAsync(default);
        var type = await service.CreateTypeAsync(new("LIMITS", "Limits", []), default);
        var target = await Category(service, "target");
        var retained = await Category(service, "retained");
        var active = await Product(service, type, "active", retained.Id);
        var deleted = await Product(service, type, "deleted");
        await service.DeleteProductAsync(deleted.Id, deleted.Version, default);

        await Assert.ThrowsAsync<NotFoundException>(() => service.SetCategoryProductsAsync(new(Guid.NewGuid(), []), default));
        await Assert.ThrowsAsync<NotFoundException>(() => service.SetCategoryProductsAsync(new(target.Id,
            [new(active.Id, active.Version, true), new(deleted.Id, deleted.Version, true)]), default));
        await Assert.ThrowsAsync<NotFoundException>(() => service.SetCategoryProductsAsync(new(target.Id,
            [new(active.Id, active.Version, true), new(Guid.NewGuid(), "missing", true)]), default));
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => service.SetCategoryProductsAsync(new(target.Id,
            Enumerable.Range(0, 1001).Select(_ => new CategoryProductChange(Guid.NewGuid(), "version", true)).ToList()), default));
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => service.SetCategoryProductsAsync(new(target.Id,
            [new(active.Id, active.Version, true), new(active.Id, active.Version, false)]), default));
        var unchanged = await service.GetAsync(active.Id, false, default);
        Assert.Equal(active.Version, unchanged.Version);
        Assert.Equal(new[] { retained.Id }, unchanged.CategoryIds);
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql", false), InlineData("mssql", true)]
    [InlineData("postgresql", false), InlineData("postgresql", true)]
    public async Task ConcurrentEditorOrDeletionAndMembershipCannotOverwriteEachOthersProductVersion(string provider, bool deleting)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        ProductDto product;
        ProductType type;
        Category target;
        Category retained;
        using (var scope = fixture.Scope(Guid.NewGuid()))
        {
            var service = Service(scope);
            await service.InitializeAsync(default);
            type = await service.CreateTypeAsync(new("CONCURRENT", "Concurrent", []), default);
            target = await Category(service, "target");
            retained = await Category(service, "retained");
            product = await Product(service, type, "concurrent", retained.Id);
        }
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<bool> Change(bool membership)
        {
            using var scope = fixture.Scope(Guid.NewGuid());
            var service = Service(scope);
            await start.Task;
            try
            {
                if (membership)
                    await service.SetCategoryProductsAsync(new(target.Id, [new(product.Id, product.Version, true)]), default);
                else if (deleting)
                    await service.DeleteProductAsync(product.Id, product.Version, default);
                else
                    await service.EditAsync(new(product.Id, product.Version, type.Version,
                        new("Edited concurrently", product.Slug, null, [retained.Id], [], [])), default);
                return true;
            }
            catch (ConflictException)
            {
                return false;
            }
            catch (NotFoundException) when (membership && deleting)
            {
                return false;
            }
        }
        var changes = new[] { Change(true), Change(false) };
        start.SetResult();
        var results = await Task.WhenAll(changes);
        Assert.Single(results, succeeded => succeeded);
        using var verify = fixture.Scope();
        if (deleting && results[1])
        {
            await Assert.ThrowsAsync<NotFoundException>(() => Service(verify).GetAsync(product.Id, false, default));
            var deleted = await verify.ServiceProvider.GetRequiredService<ApplicationDbContext>().Products
                .Include(item => item.Categories).SingleAsync(item => item.Id == product.Id);
            Assert.True(deleted.IsDeleted);
            Assert.DoesNotContain(deleted.Categories, membership => membership.CategoryId == target.Id);
            return;
        }
        var updated = await Service(verify).GetAsync(product.Id, false, default);
        Assert.Contains(retained.Id, updated.CategoryIds);
        Assert.Equal(results[0], updated.CategoryIds.Contains(target.Id));
        Assert.Equal(results[1] ? "Edited concurrently" : product.Name, updated.Name);
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task HttpMembershipRequiresProductUpdateAndRejectsMalformedBatchWithoutPartialWrites(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        const string email = "membership@example.test";
        const string password = "MembershipPassword!29874";
        var previousEmail = Environment.GetEnvironmentVariable("BOILERATE_BOOTSTRAP_EMAIL");
        var previousPassword = Environment.GetEnvironmentVariable("BOILERATE_BOOTSTRAP_PASSWORD");
        try
        {
            Environment.SetEnvironmentVariable("BOILERATE_BOOTSTRAP_EMAIL", email);
            Environment.SetEnvironmentVariable("BOILERATE_BOOTSTRAP_PASSWORD", password);
            using var bootstrap = fixture.Scope();
            await bootstrap.ServiceProvider.GetRequiredService<FoundationBootstrap>().RunAsync(["--bootstrap-admin"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable("BOILERATE_BOOTSTRAP_EMAIL", previousEmail);
            Environment.SetEnvironmentVariable("BOILERATE_BOOTSTRAP_PASSWORD", previousPassword);
        }
        using var scope = fixture.Scope();
        var service = Service(scope);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await service.InitializeAsync(default);
        var type = await service.CreateTypeAsync(new("HTTPMEMBERSHIP", "HTTP membership", []), default);
        var target = await Category(service, "target");
        var product = await Product(service, type, "product");
        await using var app = await TestHostApplication.StartAsync(fixture, true);
        using var client = app.GetTestClient();
        var path = $"/api/categories/{target.Id}/products";
        var request = new SetCategoryProductsRequest(target.Id, [new(product.Id, product.Version, true)]);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync(path, request)).StatusCode);
        var login = await client.PostAsJsonAsync("/api/tokens/get", new TokenRequest(email, password));
        login.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<TokenResponse>())!.Token);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(path, new SetCategoryProductsRequest(target.Id, [null!]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(path, request with { CategoryId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(product.Version, (await service.GetAsync(product.Id, false, default)).Version);
        var categoryFunction = await db.Functions.SingleAsync(function => function.Code == "Category");
        var update = await db.Actions.SingleAsync(action => action.Code == "Update");
        await db.Permissions.Where(grant => grant.FunctionId == categoryFunction.Id && grant.ActionId == update.Id)
            .ExecuteUpdateAsync(set => set.SetProperty(grant => grant.DeletedOn, DateTime.UtcNow));
        var response = await client.PutAsJsonAsync(path, request);
        response.EnsureSuccessStatusCode();
        product = await service.GetAsync(product.Id, false, default);
        Assert.Equal(new[] { target.Id }, product.CategoryIds);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync(path, request)).StatusCode);

        var productFunction = await db.Functions.SingleAsync(function => function.Code == "Product");
        await db.Permissions.Where(grant => grant.FunctionId == productFunction.Id && grant.ActionId == update.Id)
            .ExecuteUpdateAsync(set => set.SetProperty(grant => grant.DeletedOn, DateTime.UtcNow));
        request = new(target.Id, [new(product.Id, product.Version, false)]);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync(path, request)).StatusCode);
        Assert.Equal(new[] { target.Id }, (await service.GetAsync(product.Id, false, default)).CategoryIds);
    }
}
