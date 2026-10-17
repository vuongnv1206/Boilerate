using System.Net;
using System.Net.Http.Json;
using Ardalis.Specification.EntityFrameworkCore;
using Boilerate.Application.Common.Events;
using Boilerate.Application.Auditing;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.FileStorage;
using Boilerate.Application.Common.Models;
using Boilerate.Application.Common.Specification;
using Boilerate.Application.Identity.Tokens;
using Boilerate.Application.Identity.Users;
using Boilerate.Application.Identity.Users.Password;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Contracts;
using Boilerate.Domain.Common.Events;
using Boilerate.Domain.Identity;
using Boilerate.Infrastructure.Auth;
using Boilerate.Infrastructure.Common.Services;
using Boilerate.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Boilerate.Tests;

[Collection("Foundation")]
public sealed class WarningProviderRegressionTests
{
    private const string Password = "WarningRegression!872364";

    private static async Task<ApplicationUser> CreateUser(FoundationDatabase fixture, string name)
    {
        using var scope = fixture.Scope();
        var user = new ApplicationUser { UserName = name, Email = name + "@example.test", EmailConfirmed = true, IsActive = true };
        Assert.True((await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(user, Password)).Succeeded);
        return user;
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task StoredImagesRequireOwnershipAndUseNoSniff(string provider)
    {
        await using var fixture = new FoundationDatabase(provider);
        await fixture.CreateAsync();
        var owner = await CreateUser(fixture, "imageOwner");
        var other = await CreateUser(fixture, "otherReader");
        string path;
        using (var scope = fixture.Scope())
        {
            using var data = new MemoryStream(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
            path = await scope.ServiceProvider.GetRequiredService<IFileStorageService>()
                .UploadAsync<ApplicationUser>(new FormFile(data, 0, data.Length, "image", "photo.png"), Boilerate.Domain.Common.FileType.Image);
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.Where(u => u.Id == owner.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.ImageUrl, path));
        }

        try
        {
            await using var app = await TestHostApplication.StartAsync(fixture, catalog: false);
            using HttpClient client = app.GetTestClient();
            string url = "/files/" + path[6..];
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(url)).StatusCode);
            foreach (var user in new[] { other, owner })
            {
                using var scope = fixture.Scope();
                var token = await scope.ServiceProvider.GetRequiredService<ITokenService>().GetTokenAsync(new(user.Email!, Password), "127.0.0.1", default);
                client.DefaultRequestHeaders.Authorization = new("Bearer", token.Token);
                using var response = await client.GetAsync(url);
                Assert.Equal(user == owner ? HttpStatusCode.OK : HttpStatusCode.NotFound, response.StatusCode);
                if (user == owner)
                {
                    Assert.Equal("image/png", response.Content.Headers.ContentType!.MediaType);
                    Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
                    Assert.Equal(8, (await response.Content.ReadAsByteArrayAsync()).Length);
                }
            }
        }
        finally
        {
            using var scope = fixture.Scope();
            scope.ServiceProvider.GetRequiredService<IFileStorageService>().Remove(path);
        }
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task FilterNullNumericAndUtcDateSemanticsTranslateOnBothProviders(string provider)
    {
        await using var fixture = new FoundationDatabase(provider);
        await fixture.CreateAsync();
        using var scope = fixture.Scope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var category = new Category("Filter tests", null);
        db.Categories.Add(category);
        var type = new ProductType("FILTER", "Filter"); db.ProductTypes.Add(type);
        var first = new Product(type.Id,"Null","null"); first.AddVariant("NULL",123,null,new Dictionary<Guid,Guid>(),200);
        var second = new Product(type.Id,"Match","match","Needle"); second.AddVariant("MATCH",200,null,new Dictionary<Guid,Guid>(),200);
        db.Products.AddRange(first,second);
        await db.SaveChangesAsync();
        async Task<List<Product>> Search(string field, string operation, object? value)
        {
            var spec = new EntitiesByBaseFilterSpec<Product>(new BaseFilter { AdvancedFilter = new() { Field = field, Operator = operation, Value = value } });
            return await db.Products.AsNoTracking().WithSpecification(spec).ToListAsync();
        }

        Assert.Equal("Match", Assert.Single(await Search("Description", "contains", "needle")).Name);
        Assert.Equal("Null", Assert.Single(await Search("Description", "eq", null)).Name);
        var numericSpec = new EntitiesByBaseFilterSpec<ProductVariant>(new BaseFilter { AdvancedFilter = new() { Field = "Price", Operator = "eq", Value = "123" } });
        Assert.Single(await db.ProductVariants.WithSpecification(numericSpec).ToListAsync());
        Assert.Equal(2, (await Search("CreatedOn", "gt", "2000-01-01T07:00:00+07:00")).Count);
        Assert.Equal("Null", Assert.Single(await Search("Description", "neq", "needle")).Name);
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task InvalidAuditTypesAreRejectedRatherThanSilentlyIgnoringTheFilter(string provider)
    {
        await using var fixture = new FoundationDatabase(provider);
        await fixture.CreateAsync();
        using var scope = fixture.Scope();
        var actor = (CurrentUser)scope.ServiceProvider.GetRequiredService<Boilerate.Application.Common.Interfaces.ICurrentUser>();
        actor.SetCurrentUser(new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()) }, "test")));
        var audit = scope.ServiceProvider.GetRequiredService<IAuditService>();
        foreach (string invalid in new[] { "not-a-type", "0", "255" })
        {
            await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => audit.GetMyAuditLogsAsync(new() { Type = invalid, PageNumber = 1, PageSize = 10 }));
        }

        Assert.Empty((await audit.GetMyAuditLogsAsync(new() { Type = "Create", PageNumber = 1, PageSize = 10 })).Data);
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task PasswordRecoveryDisabledRejectsEvenValidPreviouslyIssuedResetTokens(string provider)
    {
        await using var fixture = new FoundationDatabase(provider);
        await fixture.CreateAsync();
        var user = await CreateUser(fixture, "recover");
        using var scope = fixture.Scope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var tracked = (await manager.FindByIdAsync(user.Id))!;
        string token = await manager.GeneratePasswordResetTokenAsync(tracked);
        await Assert.ThrowsAsync<ForbiddenException>(() => scope.ServiceProvider.GetRequiredService<IUserService>().ResetPasswordAsync(
            new ResetPasswordRequest { Email = user.Email, Password = "ReplacementPassword!8723", Token = token }));
        Assert.True(await manager.CheckPasswordAsync(tracked, Password));
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task DomainEventsObserveCommittedDataAndMayOpenAnotherTransaction(string provider)
    {
        await using var fixture = new FoundationDatabase(provider);
        await fixture.CreateAsync();
        using var scope = fixture.Scope();
        var publisher = Substitute.For<IEventPublisher>();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<ApplicationDbContext>>();
        await using var db = new ApplicationDbContext(options, new CurrentUser(), new NewtonSoftService(), publisher);
        var category = new Category("Event tests", null);
        db.Categories.Add(category);
        var type = new ProductType("EVENTS","Events"); db.ProductTypes.Add(type);
        var product = new Product(type.Id,"Committed","committed");
        product.AddVariant("COMMITTED",1,null,new Dictionary<Guid,Guid>(),200);
        db.Products.Add(product);
        product.DomainEvents.Add(EntityCreatedEvent.WithEntity(product));
        publisher.PublishAsync(Arg.Any<IEvent>()).Returns(async _ =>
        {
            Assert.Null(db.Database.CurrentTransaction);
            using var observer = fixture.Scope();
            Assert.True(await observer.ServiceProvider.GetRequiredService<ApplicationDbContext>().Products.AnyAsync(p => p.Id == product.Id));
            await using var inner = await db.Database.BeginTransactionAsync();
            await inner.RollbackAsync();
        });
        await db.SaveChangesAsync();
        await publisher.Received(1).PublishAsync(Arg.Any<IEvent>());
    }
}
