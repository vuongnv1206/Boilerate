using Boilerate.Application.Catalog;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Identity.Users;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using Boilerate.Domain.Inventory;
using Boilerate.Domain.Media;
using Boilerate.Domain.Messaging;
using Boilerate.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.DataProtection;
namespace Boilerate.Infrastructure.Commerce;

public sealed partial class CommerceService(ApplicationDbContext db, CommerceUnitOfWork work, ICurrentUser actor,
    IUserService users, IOptions<CommerceSettings> settings, IDataProtectionProvider protection) : ICommerceService
{
    private int MaxOptions => settings.Value.MaximumOptions;
    private int MaxVariants => settings.Value.MaximumVariants;
    private static void Version(string? expected, string current)
    {
        if (string.IsNullOrWhiteSpace(expected) || expected != current)
            throw new ConflictException("Version changed; reload before editing.");
    }
    private async Task PricePermission(CancellationToken ct)
    {
        if (!await users.HasPermissionAsync(actor.GetUserId().ToString(), "Permissions.Price.Update", ct))
            throw new ForbiddenException("Updating SKU prices requires Price.Update.");
    }
    private async Task Gate(string name, CancellationToken ct)
    {
        if (await db.Set<CommerceLock>().Where(x => x.Name == name).ExecuteUpdateAsync(s => s.SetProperty(x => x.Version, x => x.Version), ct) != 1)
            throw new ConflictException("Apply commerce migrations first.");
    }
    private async Task<ProductType> LockedType(Guid id, CancellationToken ct)
    {
        if (await db.ProductTypes.Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Version, x => x.Version), ct) != 1)
            throw new NotFoundException("Product type not found.");
        // This lock serializes product writes with schema edits without changing schema version.
        return await db.ProductTypes.Include(x => x.Fields).ThenInclude(x => x.Choices).AsSplitQuery().SingleAsync(x => x.Id == id, ct);
    }
    private IQueryable<Product> ProductGraph() => db.Products.Where(p => !p.IsDeleted).Include(x => x.Variants).ThenInclude(x => x.Selections)
        .Include(x => x.Variants).ThenInclude(x => x.Images).Include(x => x.Options).ThenInclude(x => x.Values).Include(x => x.Categories).Include(x => x.FieldValues).Include(x => x.Media).AsSplitQuery();
    private async Task<(Product Product, ProductType Type)> LockedProduct(Guid id, string version, CancellationToken ct)
    {
        var typeId = await db.Products.Where(x => x.Id == id).Select(x => (Guid?)x.ProductTypeId).SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("Product not found.");
        var type = await LockedType(typeId, ct); // Consistent lock order: type -> product -> media.
        await db.Products.Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Version, x => x.Version), ct);
        var product = await ProductGraph().SingleAsync(x => x.Id == id, ct);
        Version(version, product.Version);
        return (product, type);
    }
    public async Task<ProductDto> CreateAsync(CreateProduct r, CancellationToken ct)
    {
        if (r.Configuration is not null) return await CreateConfiguredAsync(r, ct);
        if (r.Variants is null || r.Variants.Count is < 1 || r.Variants.Count > MaxVariants || r.Options is null)
            throw new DomainException("Invalid variants/options.");
        if (r.Variants.Any(v => v.Price.HasValue || v.CompareAtPrice.HasValue))
            await PricePermission(ct);
        var id = await work.ExecuteAsync(async () =>
        {
            var type = await LockedType(r.ProductTypeId, ct);
            Version(r.ProductTypeVersion, type.Version);
            if (!type.IsActive)
                throw new ConflictException("Product type is inactive.");
            var product = new Product(type.Id, r.Content.Name, r.Content.Slug, r.Content.Description, r.FulfillmentKind);
            foreach (var input in r.Options)
            {
                var option = product.AddOption(input.Code, input.Name, MaxOptions);
                if (input.Values is null || input.Values.Count == 0)
                    throw new DomainException("An option requires values.");
                foreach (var value in input.Values)
                    option.AddValue(value.Code, value.Label);
            }
            foreach (var input in r.Variants)
                product.AddVariant(input.Sku, input.Price, input.CompareAtPrice, ResolveSelection(product, input.Options), MaxVariants);
            var states = r.Variants.Select((input, index) =>
            {
                var variant = product.Variants.ElementAt(index);
                return new VariantEditorState(variant.Id, input.Price, input.CompareAtPrice, input.IsActive, input.ImageMediaId);
            }).ToArray();
            var defaultId = r.DefaultVariantSku is null ? product.Variants.First().Id : ResolveDefault(product, r.DefaultVariantSku);
            await ApplyContent(product, type, r.Content, ct, states, defaultId);
            product.Validate(type);
            db.Products.Add(product);
            if (product.FulfillmentKind == ProductFulfillmentKind.Physical)
            {
                var location = await MainLocation(ct);
                foreach (var variant in product.Variants)
                    db.InventoryItems.Add(new InventoryItem(variant.Id, location.Id));
            }
            return product.Id;
        }, ct);
        return await GetAsync(id, false, ct);
    }
    private static Dictionary<Guid, Guid> ResolveSelection(Product p, Dictionary<string, string>? selections)
    {
        if (selections is null || selections.Count != p.Options.Count(o => !o.IsRetired))
            throw new DomainException("Invalid SKU selection.");
        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in selections)
            if (!normalized.TryAdd(pair.Key, pair.Value))
                throw new DomainException("Duplicate option selection.");
        return p.Options.Where(o => !o.IsRetired).ToDictionary(o => o.Id, o =>
        {
            if (!normalized.TryGetValue(o.Code, out var code))
                throw new DomainException("An option selection is missing.");
            return o.Values.SingleOrDefault(v => string.Equals(v.Code, code, StringComparison.OrdinalIgnoreCase))?.Id ?? throw new DomainException("Unknown option value.");
        });
    }
    private async Task ApplyContent(Product p, ProductType type, ProductContent content, CancellationToken ct,
        IReadOnlyCollection<VariantEditorState>? variantStates = null, Guid? defaultVariantId = null)
    {
        if (content.CategoryIds is null || content.Fields is null || content.Media is null || content.Fields.Count > 100 || content.Media.Count > (variantStates is null ? 30 : Math.Max(30, variantStates.Count * 31)) || content.CategoryIds.Count > 100)
            throw new DomainException("Invalid product content.");
        var categoryIds = content.CategoryIds.Distinct().ToArray();
        if (await db.Categories.CountAsync(c => categoryIds.Contains(c.Id), ct) != categoryIds.Length)
            throw new DomainException("Unknown category.");
        var mediaIds = p.Media.Select(m => m.MediaAssetId).Concat(content.Media.Select(m => m.MediaAssetId)).Distinct().Order().ToArray();
        foreach (var id in mediaIds)
        {
            if (await db.MediaAssets.Where(m => m.Id == id).ExecuteUpdateAsync(s => s.SetProperty(m => m.Version, m => m.Version), ct) != 1)
                throw new DomainException("Unknown media asset.");
            var asset = await db.MediaAssets.SingleAsync(m => m.Id == id, ct);
            if (content.Media.Any(m => m.MediaAssetId == id))
                asset.Attach();
        }
        foreach (var detached in p.Media.Where(m => content.Media.All(n => n.MediaAssetId != m.MediaAssetId)).ToArray())
        {
            db.Set<ProductMedia>().Remove(detached);
            db.OutboxMessages.Add(OutboxMessage.Cleanup(detached.MediaAssetId, actor.GetUserId(), System.Diagnostics.Activity.Current?.Id));
        }
        db.Set<ProductCategory>().RemoveRange(p.Categories.Where(c => !categoryIds.Contains(c.CategoryId)).ToArray());
        db.Set<ProductFieldValue>().RemoveRange(p.FieldValues.Where(v => content.Fields.All(n => n.FieldDefinitionId != v.FieldDefinitionId)).ToArray());
        p.Edit(content.Name, content.Slug, content.Description);
        p.SetCategories(categoryIds);
        p.SetFields(type, content.Fields.Select(f => f.ToValue(p.Id, type.Id)));
        var gallery = content.Media.Select(m => new ProductMedia(p.Id, m.MediaAssetId, m.SortOrder, m.AltText)).ToArray();
        if (variantStates is null)
            p.SetMedia(gallery);
        else
            p.ApplyEditorState(variantStates, gallery, defaultVariantId!.Value);
    }
    public async Task<ProductDto> EditAsync(EditProduct r, CancellationToken ct)
    {
        await work.ExecuteAsync(async () =>
        {
            var (p, t) = await LockedProduct(r.Id, r.Version, ct);
            Version(r.ProductTypeVersion, t.Version);
            await ApplyContent(p, t, r.Content, ct);
            p.Validate(t);
            return true;
        }, ct);
        return await GetAsync(r.Id, false, ct);
    }
    public async Task<ProductDto> StatusAsync(ChangeProductStatus r, CancellationToken ct)
    {
        await work.ExecuteAsync(async () => { var (p, t) = await LockedProduct(r.Id, r.Version, ct); p.ChangeStatus(r.Status, t); p.Validate(t); return true; }, ct);
        return await GetAsync(r.Id, false, ct);
    }
    public async Task<ProductDto> AddVariantAsync(AddVariant r, CancellationToken ct)
    {
        if (r.Variant.Price.HasValue || r.Variant.CompareAtPrice.HasValue)
            await PricePermission(ct);
        await work.ExecuteAsync(async () =>
        {
            var (p, t) = await LockedProduct(r.ProductId, r.Version, ct);
            var v = p.AddVariant(r.Variant.Sku, r.Variant.Price, r.Variant.CompareAtPrice, ResolveSelection(p, r.Variant.Options), MaxVariants);
            p.EditVariant(v.Id, r.Variant.Price, r.Variant.CompareAtPrice, r.Variant.IsActive, r.Variant.ImageMediaId);
            if (p.FulfillmentKind == ProductFulfillmentKind.Physical)
            {
                var location = await MainLocation(ct);
                db.InventoryItems.Add(new InventoryItem(v.Id, location.Id));
            }
            p.Validate(t);
            return true;
        }, ct);
        return await GetAsync(r.ProductId, false, ct);
    }
    public async Task<ProductDto> EditVariantAsync(EditVariant r, CancellationToken ct)
    {
        await work.ExecuteAsync(async () =>
        {
            var (p, t) = await LockedProduct(r.ProductId, r.Version, ct);
            var v = p.GetVariant(r.VariantId);
            if (v.Price != r.Price || v.CompareAtPrice != r.CompareAtPrice)
                await PricePermission(ct);
            p.EditVariant(v.Id, r.Price, r.CompareAtPrice, r.IsActive, r.ImageMediaId);
            p.Validate(t);
            return true;
        }, ct);
        return await GetAsync(r.ProductId, false, ct);
    }
    public async Task<ProductDto> DefaultAsync(SetDefaultVariant r, CancellationToken ct)
    {
        await work.ExecuteAsync(async () =>
        {
            var (p, t) = await LockedProduct(r.ProductId, r.Version, ct);
            var selected = p.GetVariant(r.VariantId);
            if (!selected.IsActive)
                throw new DomainException("Default SKU must be active.");
            // Clear the old flag first to satisfy the filtered unique index during the switch.
            await db.ProductVariants.Where(v => v.ProductId == p.Id && v.IsDefault).ExecuteUpdateAsync(s => s.SetProperty(v => v.IsDefault, false), ct);
            p.SelectDefault(r.VariantId);
            foreach (var v in p.Variants)
                db.Entry(v).Property(x => x.IsDefault).IsModified = true;
            p.Validate(t);
            return true;
        }, ct);
        return await GetAsync(r.ProductId, false, ct);
    }
    public async Task<ProductDto> OptionAsync(EditOption r, CancellationToken ct)
    {
        await work.ExecuteAsync(async () =>
        {
            var (p, t) = await LockedProduct(r.ProductId, r.Version, ct);
            var option = p.Options.SingleOrDefault(o => o.Id == r.OptionId) ?? throw new NotFoundException("Option not found.");
            option.Rename(r.Name, r.SortOrder);
            foreach (var add in r.AddValues)
                option.AddValue(add.Code, add.Label);
            foreach (var edit in r.Values)
                (option.Values.SingleOrDefault(v => v.Id == edit.Id) ?? throw new DomainException("Unknown option value.")).Rename(edit.Label, edit.SortOrder);
            p.Touch();
            p.Validate(t);
            return true;
        }, ct);
        return await GetAsync(r.ProductId, false, ct);
    }
    public List<Dictionary<string, string>> Preview(PreviewVariants r)
    {
        if (r.Options is null || r.Options.Count > MaxOptions)
            throw new DomainException("Option limit exceeded.");
        var sample = new Product(Guid.NewGuid(), "Preview", "preview");
        var result = new List<Dictionary<string, string>> { new() };
        foreach (var input in r.Options)
        {
            var option = sample.AddOption(input.Code, input.Name, MaxOptions);
            if (input.Values is null || input.Values.Count == 0 || (long)result.Count * input.Values.Count > MaxVariants)
                throw new DomainException("Invalid options or combination limit exceeded.");
            foreach (var value in input.Values)
                option.AddValue(value.Code, value.Label);
            result = result.SelectMany(existing => option.Values.Select(value => { var next = new Dictionary<string, string>(existing); next.Add(option.Code, value.Code); return next; })).ToList();
        }
        return result;
    }
    public Task<Category> CategoryAsync(SaveCategory r, CancellationToken ct) => work.ExecuteAsync(async () =>
    {
        await Gate("categories", ct);
        var category = r.Id.HasValue ? await db.Categories.SingleOrDefaultAsync(c => c.Id == r.Id, ct) ?? throw new NotFoundException("Category not found.") : new Category(r.Name, r.Description, r.Slug);
        if (r.Id.HasValue)
            Version(r.Version, category.Version);
        var ancestors = new HashSet<Guid>();
        Guid? parent = r.ParentId;
        while (parent.HasValue)
        {
            if (!ancestors.Add(parent.Value))
                throw new ConflictException("Existing category cycle.");
            var node = await db.Categories.SingleOrDefaultAsync(c => c.Id == parent, ct) ?? throw new DomainException("Unknown parent.");
            parent = node.ParentId;
        }
        category.Edit(r.Name, r.Slug, r.Description, r.ParentId, r.SortOrder, r.IsActive, ancestors);
        if (!r.Id.HasValue)
            db.Categories.Add(category);
        return category;
    }, ct);
    public Task<bool> DeleteCategoryAsync(DeleteCategory r, CancellationToken ct) => work.ExecuteAsync(async () =>
    {
        await Gate("categories", ct);
        var category = await db.Categories.SingleOrDefaultAsync(c => c.Id == r.Id, ct) ?? throw new NotFoundException("Category not found.");
        Version(r.Version, category.Version);
        if (await db.Categories.AnyAsync(c => c.ParentId == r.Id, ct) || await db.Set<ProductCategory>().AnyAsync(c => c.CategoryId == r.Id, ct))
            throw new ConflictException("Category is still referenced.");
        db.Categories.Remove(category);
        return true;
    }, ct);
    private static void AddFields(ProductType type, IEnumerable<FieldDefinitionInput> fields, bool used)
    {
        foreach (var input in fields)
        {
            var field = type.AddField(input.Code, input.DataType, input.Label, input.HelpText, input.IsRequired, input.IsFilterable, used);
            foreach (var choice in input.Choices)
                field.AddChoice(choice.Code, choice.Label);
        }
    }
    public Task<ProductType> CreateTypeAsync(CreateProductType r, CancellationToken ct) => work.ExecuteAsync(() =>
    {
        var type = new ProductType(r.Code, r.Name);
        AddFields(type, r.Fields, false);
        db.ProductTypes.Add(type);
        return Task.FromResult(type);
    }, ct);
    public Task<ProductType> EditTypeAsync(EditProductType r, CancellationToken ct) => work.ExecuteAsync(async () =>
    {
        var type = await LockedType(r.Id, ct);
        Version(r.Version, type.Version);
        if (!r.IsActive && await db.Products.AnyAsync(p => p.ProductTypeId == type.Id && p.Status == ProductStatus.Published, ct))
            throw new ConflictException("Unpublish products before deactivating their type.");
        type.Edit(r.Name, r.IsActive);
        AddFields(type, r.AddFields, await db.Products.AnyAsync(p => p.ProductTypeId == type.Id, ct));
        foreach (var edit in r.Fields)
        {
            var field = type.Fields.SingleOrDefault(f => f.Id == edit.Id) ?? throw new DomainException("Unknown field.");
            if (!field.IsActive && edit.IsActive && field.IsRequired &&
                await db.Products.AnyAsync(p => p.ProductTypeId == type.Id && p.Status == ProductStatus.Published && !p.FieldValues.Any(v => v.FieldDefinitionId == field.Id),ct))
                throw new ConflictException("Published products must have values before reactivating a required field.");
            field.Edit(edit.Label, edit.HelpText, edit.IsFilterable, edit.IsActive, edit.SortOrder);
            foreach (var choice in edit.AddChoices)
                field.AddChoice(choice.Code, choice.Label);
            foreach (var choice in edit.Choices)
                (field.Choices.SingleOrDefault(c => c.Id == choice.Id) ?? throw new DomainException("Unknown choice.")).Edit(choice.Label, choice.IsActive, choice.SortOrder);
        }
        return type;
    }, ct);
    private async Task<InventoryLocation> MainLocation(CancellationToken ct) =>
        await db.Set<InventoryLocation>().SingleOrDefaultAsync(l => l.Code == "MAIN" && l.IsActive, ct)
        ?? throw new ConflictException("Initialize commerce before creating SKUs.");
    public Task<Guid> InitializeAsync(CancellationToken ct) => work.ExecuteAsync(async () =>
    {
        await Gate("initialization", ct);
        var existing = await db.Set<InventoryLocation>().SingleOrDefaultAsync(l => l.Code == "MAIN", ct);
        if (existing is not null)
            return existing.Id;
        var main = new InventoryLocation("MAIN", "Main warehouse");
        db.Add(main);
        return main.Id;
    }, ct);
    public async Task<MovementDto> AdjustAsync(AdjustInventory r, CancellationToken ct)
    {
        async Task<StockMovement?> Existing() => await db.StockMovements.AsNoTracking().SingleOrDefaultAsync(m => m.OperationId == r.OperationId, ct);
        MovementDto Replay(StockMovement m)
        {
            if (m.InventoryItemId != r.InventoryItemId || m.Delta != r.Delta || m.Reason != r.Reason.Trim())
                throw new ConflictException("OperationId was already used with different content.");
            return MapMovement(m);
        }
        _ = CatalogRules.Required(r.Reason, 1000, "Reason");
        if (r.OperationId == Guid.Empty)
            throw new DomainException("OperationId is required.");
        var existing = await Existing();
        if (existing is not null)
            return Replay(existing);
        try
        {
            return await work.ExecuteAsync(async () =>
            {
                // Separate reads release their read locks before looking up the parent; a joined
                // balance/product lookup could itself recreate the inverse lock dependency.
                var variantId = await db.InventoryItems.Where(i => i.Id == r.InventoryItemId).Select(i => (Guid?)i.VariantId)
                    .SingleOrDefaultAsync(ct) ?? throw new NotFoundException("Inventory item not found.");
                var productId = await db.ProductVariants.Where(v => v.Id == variantId).Select(v => v.ProductId).SingleAsync(ct);
                var source = await db.Products.Where(p => p.Id == productId).Select(p => new { p.Id, p.ProductTypeId }).SingleAsync(ct);
                // Ordering locks type -> product -> balance too; never read a locked product while holding only its balance.
                await LockedType(source.ProductTypeId, ct);
                await db.Products.Where(p => p.Id == source.Id).ExecuteUpdateAsync(s => s.SetProperty(p => p.Version, p => p.Version), ct);
                if (!await db.Products.AnyAsync(p => p.Id == source.Id && p.FulfillmentKind == ProductFulfillmentKind.Physical, ct))
                    throw new ConflictException("Only physical SKUs use warehouse adjustments.");
                if (await db.InventoryItems.Where(i => i.Id == r.InventoryItemId).ExecuteUpdateAsync(s => s.SetProperty(i => i.Version, i => i.Version), ct) != 1)
                    throw new NotFoundException("Inventory item not found.");
                var prior = await Existing();
                if (prior is not null)
                    return Replay(prior);
                var item = await db.InventoryItems.SingleAsync(i => i.Id == r.InventoryItemId, ct);
                Version(r.Version, item.Version);
                var movement = item.Adjust(r.OperationId, r.Delta, r.Reason, actor.GetUserId());
                db.StockMovements.Add(movement);
                return MapMovement(movement);
            }, ct);
        }
        catch (DbUpdateException)
        {
            var prior = await Existing();
            if (prior is not null)
                return Replay(prior);
            throw;
        }
    }
    public Task<bool> ReplayAsync(ReplayOutbox r, CancellationToken ct) => work.ExecuteAsync(async () =>
    {
        var message = await db.OutboxMessages.SingleOrDefaultAsync(m => m.MessageId == r.MessageId, ct) ?? throw new NotFoundException("Message not found.");
        if (message.State != OutboxState.DeadLetter)
            throw new ConflictException("Only dead-letter messages can be replayed.");
        int changed = await db.OutboxMessages.Where(m => m.MessageId == r.MessageId && m.State == OutboxState.DeadLetter)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.State, OutboxState.Pending).SetProperty(m => m.Attempts, 0).SetProperty(m => m.NextAttemptAt, DateTime.UtcNow).SetProperty(m => m.LeaseToken, (Guid?)null).SetProperty(m => m.LeaseUntil, (DateTime?)null), ct);
        if (changed != 1)
            throw new ConflictException("Message was already replayed.");
        db.Trails.Add(new Boilerate.Domain.Auditing.Trail
        {
            UserId = actor.GetUserId(),
            TableName = "OutboxMessages",
            PrimaryKey = r.MessageId.ToString(),
            Type = Boilerate.Domain.Auditing.TrailType.Update,
            DateTime = DateTime.UtcNow,
            OldValues = "{\"State\":\"DeadLetter\"}",
            NewValues = "{\"State\":\"Pending\",\"Source\":\"OperatorReplay\"}"
        });
        return true;
    }, ct);
}
