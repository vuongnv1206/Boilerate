using System.Net;
using System.Net.Http.Json;
using Boilerate.Application.Catalog.Products;
using Boilerate.Application.Catalog.ProductTypes;
using Boilerate.Application.Identity.Tokens;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Identity;
using Boilerate.Infrastructure.Persistence.Context;
using Boilerate.Infrastructure.Persistence.Initialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Boilerate.Tests;

[Collection("Foundation")]
public sealed class ProductTypeWorkspaceTests
{
    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task HttpDuplicateCreatesIndependentSchemaIdentitiesAndKeepsTypePermissionsSeparate(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        const string email = "type-workspace@example.test";
        const string password = "ProductTypeWorkspace!29874";
        using (var scope = fixture.Scope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
            Assert.True((await roles.CreateAsync(new ApplicationRole("Admin", "Type administrator"))).Succeeded);
            var user = new ApplicationUser { UserName = email, Email = email, IsActive = true, EmailConfirmed = true };
            Assert.True((await users.CreateAsync(user, password)).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, "Admin")).Succeeded);
            await scope.ServiceProvider.GetRequiredService<FoundationBootstrap>().RunAsync(["--register-capabilities", "--grant-admin-capabilities"]);
        }
        await using var app = await TestHostApplication.StartAsync(fixture, true);
        using var client = app.GetTestClient();
        var login = await client.PostAsJsonAsync("/api/tokens/get", new TokenRequest(email, password));
        login.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<TokenResponse>())!.Token);
        var input = new CreateProductTypeRequest("SCHEMA", "Schema", [new("LEVEL", ProductFieldType.Choice, "Level", "Pick a level", false, true,
            [new("BEGINNER", "Beginner"), new("ADVANCED", "Advanced")])]);
        var created = await client.PostAsJsonAsync("/api/product-types", input);
        created.EnsureSuccessStatusCode();
        var original = (await created.Content.ReadFromJsonAsync<ProductTypeWire>())!;
        var copiedResponse = await client.PostAsJsonAsync("/api/product-types", input with { Code = "SCHEMA-COPY", Name = "Schema copy" });
        copiedResponse.EnsureSuccessStatusCode();
        var copied = (await copiedResponse.Content.ReadFromJsonAsync<ProductTypeWire>())!;
        Assert.NotEqual(original.Id, copied.Id);
        var originalField = Assert.Single(original.Fields);
        var copiedField = Assert.Single(copied.Fields);
        Assert.NotEqual(originalField.Id, copiedField.Id);
        Assert.Empty(originalField.Choices.Select(choice => choice.Id).Intersect(copiedField.Choices.Select(choice => choice.Id)));
        Assert.Equal(originalField.Choices.Select(choice => choice.Label).Order(), copiedField.Choices.Select(choice => choice.Label).Order());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/product-types", input with { Code = "schema" })).StatusCode);

        var edit = new UpdateProductTypeRequest(original.Id, original.Version, "Changed original", true, [],
            [new(originalField.Id, "Changed level", null, true, true, 0, [], [])]);
        var changed = await client.PutAsJsonAsync($"/api/product-types/{original.Id}", edit);
        changed.EnsureSuccessStatusCode();
        var updated = (await changed.Content.ReadFromJsonAsync<ProductTypeWire>())!;
        Assert.NotEqual(original.Version, updated.Version);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/product-types/{original.Id}", edit)).StatusCode);
        var reloadedCopy = (await client.GetFromJsonAsync<ProductTypeWire>($"/api/product-types/{copied.Id}"))!;
        Assert.Equal(copied.Version, reloadedCopy.Version);
        Assert.Equal("Schema copy", reloadedCopy.Name);
        Assert.Equal("Level", Assert.Single(reloadedCopy.Fields).Label);

        using (var scope = fixture.Scope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var functionId = await db.Functions.Where(function => function.Code == "Product").Select(function => function.Id).SingleAsync();
            var actionId = await db.Actions.Where(action => action.Code == "View").Select(action => action.Id).SingleAsync();
            await db.Permissions.Where(grant => grant.FunctionId == functionId && grant.ActionId == actionId)
                .ExecuteUpdateAsync(set => set.SetProperty(grant => grant.DeletedOn, DateTime.UtcNow));
        }
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/product-types")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/product-types/{original.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/products/search", new ProductSearch())).StatusCode);
    }

    private sealed record ProductTypeWire(Guid Id, string Version, string Name, List<FieldWire> Fields);
    private sealed record FieldWire(Guid Id, string Label, List<ChoiceWire> Choices);
    private sealed record ChoiceWire(Guid Id, string Label);
}
