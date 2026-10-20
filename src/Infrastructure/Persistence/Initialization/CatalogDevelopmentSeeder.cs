using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Boilerate.Application.Catalog;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Identity.Users;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Identity;
using Boilerate.Infrastructure.Auth;
using Boilerate.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Boilerate.Infrastructure.Persistence.Initialization;

/// <summary>Additive sample data through the same Catalog use cases as the API; existing records are left intact.</summary>
public sealed class CatalogDevelopmentSeeder(
    ApplicationDbContext db,
    ICommerceService commerce,
    UserManager<ApplicationUser> users,
    IUserService permissions,
    ICurrentUser currentUser,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<CatalogDevelopmentSeeder> logger) : ICustomSeeder
{
    private static readonly string[] RequiredPermissions =
    [
        "Permissions.ProductType.Create", "Permissions.Category.Create", "Permissions.Product.Create",
        "Permissions.Product.Publish", "Permissions.Price.Update", "Permissions.Inventory.Initialize", "Permissions.Inventory.Adjust"
    ];

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (!environment.IsDevelopment())
            throw new InvalidOperationException("Catalog development data cannot be seeded outside Development.");
        cancellationToken.ThrowIfCancellationRequested();
        var data = await CatalogSeedData.ReadAsync(cancellationToken);
        await InitializeActorAsync(cancellationToken);
        var locationId = await commerce.InitializeAsync(cancellationToken);
        var types = await EnsureTypesAsync(data.Types, cancellationToken);
        var categories = await EnsureCategoriesAsync(data.Categories, cancellationToken);
        var created = 0;
        foreach (var sample in data.Products)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // SKU survives slug/name edits. Slug also prevents taking over an existing non-demo record.
            if (await db.Products.AnyAsync(product => product.NormalizedSlug == sample.Slug ||
                product.Variants.Any(variant => variant.NormalizedSku == sample.Sku), cancellationToken))
                continue;

            var type = types[sample.TypeCode];
            var product = await commerce.CreateAsync(new(type.Id, type.Version,
                new(sample.Name, sample.Slug, data.Description, [categories[sample.CategorySlug].Id], [], []),
                [], [new(sample.Sku, sample.Price, null, [])]), cancellationToken);

            var variant = product.Variants.Single();
            var inventory = await db.InventoryItems.AsNoTracking().SingleAsync(item =>
                item.VariantId == variant.Id && item.LocationId == locationId, cancellationToken);
            var operationId = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"catalog-development-v1:stock:{sample.Sku}"))[..16]);
            await commerce.AdjustAsync(new(inventory.Id, inventory.Version, operationId, sample.Stock,
                "Development Catalog sample opening stock"), cancellationToken);
            if (sample.Status != ProductStatus.Draft)
                await commerce.StatusAsync(new(product.Id, product.Version, sample.Status), cancellationToken);
            created++;
            logger.LogInformation("Created development sample SKU {Sku}", sample.Sku);
        }

        logger.LogInformation("Catalog development data: {Created} products created, {Skipped} existing products preserved",
            created, data.Products.Length - created);
    }

    private async Task InitializeActorAsync(CancellationToken cancellationToken)
    {
        var email = configuration["DevelopmentSeed:ActorEmail"];
        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException("Set DevelopmentSeed:ActorEmail to an existing active, authorized operator before running --seed-dev.");
        var user = await users.FindByEmailAsync(email.Trim());
        if (user is not { IsActive: true } || !Guid.TryParse(user.Id, out var actorId) || actorId == Guid.Empty)
            throw new InvalidOperationException("The development seed actor must be an existing active user.");
        foreach (var permission in RequiredPermissions)
            if (!await permissions.HasPermissionAsync(user.Id, permission, cancellationToken))
                throw new ForbiddenException($"The development seed actor requires {permission}; no grants were changed.");

        if (currentUser.GetUserId() == Guid.Empty)
            ((ICurrentUserInitializer)currentUser).SetCurrentUser(new ClaimsPrincipal(new ClaimsIdentity(
                [new(ClaimTypes.NameIdentifier, user.Id), new(ClaimTypes.Email, user.Email!)], "DevelopmentSeeder")));
        else if (currentUser.GetUserId() != actorId)
            throw new InvalidOperationException("The seed actor differs from the current scope identity.");
    }

    private async Task<Dictionary<string, ProductType>> EnsureTypesAsync(ProductTypeSeed[] samples, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, ProductType>(StringComparer.OrdinalIgnoreCase);
        foreach (var sample in samples)
        {
            var type = await db.ProductTypes.Include(type => type.Fields)
                .SingleOrDefaultAsync(type => type.NormalizedCode == sample.Code, cancellationToken)
                ?? await commerce.CreateTypeAsync(new(sample.Code, sample.Name, []), cancellationToken);
            if (!type.IsActive || type.Fields.Any(field => field.IsActive && field.IsRequired))
                throw new InvalidOperationException($"Seed type {sample.Code} was customized; use a separate sample dataset instead of overwriting it.");
            result.Add(sample.Code, type);
        }
        return result;
    }

    private async Task<Dictionary<string, Category>> EnsureCategoriesAsync(CategorySeed[] samples, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, Category>(StringComparer.Ordinal);
        foreach (var sample in samples)
        {
            var category = await db.Categories.SingleOrDefaultAsync(category => category.NormalizedSlug == sample.Slug, cancellationToken)
                ?? await commerce.CategoryAsync(new(null, null, sample.Name, sample.Slug,
                    $"Những thiết kế nổi bật thuộc danh mục {sample.Name.ToLowerInvariant()}.", null, result.Count, true), cancellationToken);
            result.Add(sample.Slug, category);
        }
        return result;
    }
}
