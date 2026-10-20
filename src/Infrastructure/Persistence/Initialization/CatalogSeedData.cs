using System.Text.Json;
using System.Text.Json.Serialization;
using Boilerate.Domain.Catalog;

namespace Boilerate.Infrastructure.Persistence.Initialization;

internal sealed record CatalogSeedData(
    string Description, ProductTypeSeed[] Types, CategorySeed[] Categories, ProductSeed[] Products)
{
    internal static async Task<CatalogSeedData> ReadAsync(CancellationToken cancellationToken)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "SeedData", "catalog.json");
        await using var file = File.OpenRead(path);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            RespectRequiredConstructorParameters = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            Converters = { new JsonStringEnumConverter<ProductStatus>(allowIntegerValues: false) }
        };
        var data = await JsonSerializer.DeserializeAsync<CatalogSeedData>(file, options, cancellationToken)
            ?? throw new InvalidOperationException("SeedData/catalog.json is empty.");
        data.Validate();
        return data;
    }

    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(Description) || Types is not { Length: > 0 } ||
            Categories is not { Length: > 0 } || Products is not { Length: > 0 })
            throw new InvalidOperationException("catalog.json requires description, types, categories and products.");
        if (Types.Any(type => type is null || string.IsNullOrWhiteSpace(type.Code) || string.IsNullOrWhiteSpace(type.Name)) ||
            Categories.Any(category => category is null || string.IsNullOrWhiteSpace(category.Slug) || string.IsNullOrWhiteSpace(category.Name)) ||
            Products.Any(product => product is null || string.IsNullOrWhiteSpace(product.Sku) || string.IsNullOrWhiteSpace(product.Slug) ||
                string.IsNullOrWhiteSpace(product.Name) || product.Price < 0 || product.Stock < 0 || !Enum.IsDefined(product.Status)))
            throw new InvalidOperationException("catalog.json contains an invalid record.");
        if (Types.Select(type => type.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Types.Length ||
            Categories.Select(category => category.Slug).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Categories.Length ||
            Products.Select(product => product.Sku).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Products.Length ||
            Products.Select(product => product.Slug).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Products.Length)
            throw new InvalidOperationException("catalog.json contains duplicate codes, slugs or SKUs.");
        if (Products.Any(product => !Types.Any(type => type.Code == product.TypeCode) ||
            !Categories.Any(category => category.Slug == product.CategorySlug)))
            throw new InvalidOperationException("A product in catalog.json refers to a missing type or category.");
    }
}

internal sealed record ProductTypeSeed(string Code, string Name);
internal sealed record CategorySeed(string Slug, string Name);
internal sealed record ProductSeed(string Name, string Slug, string CategorySlug, string TypeCode,
    string Sku, decimal Price, int Stock, ProductStatus Status);
