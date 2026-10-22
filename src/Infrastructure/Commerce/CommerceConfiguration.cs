using Boilerate.Application.Catalog;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using Boilerate.Domain.Inventory;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Infrastructure.Commerce;

public sealed partial class CommerceService
{
    private async Task<ProductDto> CreateConfiguredAsync(CreateProduct request, CancellationToken ct)
    {
        var id = await work.ExecuteAsync(async () =>
        {
            var type = await LockedType(request.ProductTypeId, ct);
            Version(request.ProductTypeVersion, type.Version);
            if (!type.IsActive) throw new ConflictException("Product type is inactive.");
            var product = new Product(type.Id, request.Content.Name, request.Content.Slug, request.Content.Description, request.FulfillmentKind);
            db.Products.Add(product);
            await ApplyConfiguration(product, type, request.Content, request.Configuration!, true, ct);
            return product.Id;
        }, ct);
        return await GetAsync(id, false, ct);
    }

    private async Task<ProductDto> SaveConfiguredAsync(SaveProductEditor request, CancellationToken ct)
    {
        await work.ExecuteAsync(async () =>
        {
            var (product, type) = await LockedProduct(request.Id, request.Version, ct);
            Version(request.ProductTypeVersion, type.Version);
            await ApplyConfiguration(product, type, request.Content, request.Configuration!, false, ct);
            return true;
        }, ct);
        return await GetAsync(request.Id, false, ct);
    }

    private async Task ApplyConfiguration(Product product, ProductType type, ProductContent content, CatalogConfiguration configuration, bool creating, CancellationToken ct)
    {
        var original = product.Variants.Where(v => !v.IsRetired).ToDictionary(v => v.Id);
        if (configuration.Options is null || configuration.Variants is null || configuration.Options.Count > 10 ||
            configuration.Variants.Count is < 1 or > 1000 ||
            configuration.Options.Any(o => o is null || o.Id == Guid.Empty || o.Values is null || o.Values.Count == 0 || o.Values.Count > 200 ||
                o.Values.Any(v => v is null || v.Id == Guid.Empty) || o.Values.Select(v => v.Id).Distinct().Count() != o.Values.Count ||
                o.Values.Select(v => v.Label?.Trim().ToUpperInvariant()).Distinct().Count() != o.Values.Count) ||
            configuration.Variants.Any(v => v is null || v.Id == Guid.Empty || v.Selections is null) ||
            configuration.Options.Select(o => o.Id).Distinct().Count() != configuration.Options.Count ||
            configuration.Options.Select(o => o.Name?.Trim().ToUpperInvariant()).Distinct().Count() != configuration.Options.Count ||
            configuration.Variants.Select(v => v.Id).Distinct().Count() != configuration.Variants.Count ||
            !configuration.Variants.Any(v => v.Id == configuration.DefaultVariantId && v.IsActive))
            throw new DomainException("Kiểm tra tên, giá trị lựa chọn và biến thể mặc định.");
        var existingOptions = product.Options.Where(option => !option.IsRetired).Select(option => option.Id).ToHashSet();
        if (configuration.Options.Count > 3 && (configuration.Options.Count > existingOptions.Count || configuration.Options.Any(option => !existingOptions.Contains(option.Id))))
            throw new DomainException("Mỗi sản phẩm có tối đa ba nhóm mới. Các nhóm cũ vẫn được giữ nguyên.");
        if (configuration.Variants.Count > 50 &&
            (configuration.Variants.Count > original.Count || configuration.Variants.Any(v => !original.ContainsKey(v.Id))))
            throw new DomainException("Mỗi sản phẩm có tối đa 50 biến thể mới. Các biến thể cũ vẫn được giữ nguyên.");
        var combinations = configuration.Variants.Select(v => string.Join(";", v.Selections.OrderBy(s => s.Key).Select(s => $"{s.Key:N}={s.Value:N}"))).ToArray();
        if (combinations.Distinct().Count() != combinations.Length)
            throw new DomainException("Hai biến thể không thể có cùng tổ hợp lựa chọn.");
        if (configuration.Variants.Any(v => original.TryGetValue(v.Id, out var old)
                ? old.Price != v.Price || old.CompareAtPrice != v.CompareAtPrice
                : v.Price.HasValue || v.CompareAtPrice.HasValue))
            await PricePermission(ct);

        var options = new Dictionary<Guid, ProductOption>();
        var values = new Dictionary<Guid, ProductOptionValue>();
        foreach (var (input, index) in configuration.Options.Select((o, i) => (o, i)))
        {
            var option = product.Options.SingleOrDefault(o => o.Id == input.Id && !o.IsRetired)
                ?? product.AddOption(null!, input.Name, Math.Max(3, product.Options.Count + configuration.Options.Count));
            option.Rename(input.Name, index);
            options.Add(input.Id, option);
            foreach (var (choice, order) in input.Values.Select((v, i) => (v, i)))
            {
                var value = option.Values.SingleOrDefault(v => v.Id == choice.Id && !v.IsRetired) ?? option.AddValue(null!, choice.Label);
                value.Rename(choice.Label, order);
                if (!values.TryAdd(choice.Id, value)) throw new DomainException("Giá trị lựa chọn bị lặp.");
            }
            foreach (var value in option.Values.Where(v => !values.Values.Any(n => n.Id == v.Id))) value.Retire();
        }
        foreach (var option in product.Options.Where(o => !options.Values.Any(n => n.Id == o.Id))) option.Retire();
        var desiredIds = configuration.Variants.Select(v => v.Id).ToHashSet();
        foreach (var variant in original.Values.Where(v => !desiredIds.Contains(v.Id))) variant.Retire();
        // Release unique default/combination indexes before replacing the final configuration.
        product.ClearConfigurationDefault();
        if (!creating)
        {
            await db.ProductVariants.Where(v => v.ProductId == product.Id).ExecuteUpdateAsync(s => s.SetProperty(v => v.IsDefault, false), ct);
            foreach (var variant in original.Values) db.Entry(variant).Property(v => v.IsDefault).IsModified = true;
            await db.SaveChangesAsync(ct);
        }

        var resolved = new Dictionary<Guid, ProductVariant>();
        foreach (var input in configuration.Variants)
        {
            if (input.Selections.Count != options.Count || input.Selections.Any(s => !options.ContainsKey(s.Key) ||
                !values.TryGetValue(s.Value, out var value) || value.OptionId != options[s.Key].Id))
                throw new DomainException("Mỗi biến thể cần chọn một giá trị từ mỗi nhóm lựa chọn.");
            var selections = input.Selections.ToDictionary(s => options[s.Key].Id, s => values[s.Value].Id);
            ProductVariant variant;
            if (original.TryGetValue(input.Id, out var current))
            {
                variant = current;
                if (variant.Selections.Count != selections.Count || variant.Selections.Any(s => !selections.TryGetValue(s.OptionId, out var value) || value != s.OptionValueId))
                {
                    await db.Set<VariantOptionValue>().Where(s => s.VariantId == variant.Id).ExecuteDeleteAsync(ct);
                    foreach (var old in variant.Selections) db.Entry(old).State = EntityState.Detached;
                    variant.ReplaceSelection(options.Values.ToArray(), selections);
                }
            }
            else
                variant = product.AddVariant(input.Sku ?? "", input.Price, input.CompareAtPrice, selections, Math.Max(50, original.Count + configuration.Variants.Count));
            var images = input.Images ?? [];
            db.Set<VariantMedia>().RemoveRange(variant.Images.Where(image => images.All(n => n.MediaAssetId != image.MediaAssetId)).ToArray());
            variant.Configure(input.Sku, input.TrackInventory, input.Weight, input.Width, input.Height, input.Length,
                images.Select((image, order) => new VariantMedia(variant.Id, image.MediaAssetId, order, image.AltText)));
            resolved.Add(input.Id, variant);
        }
        var media = content.Media.Concat(configuration.Variants.SelectMany(v => v.Images ?? []))
            .Concat(configuration.Variants.Where(v => v.ImageMediaId.HasValue).Select(v => new MediaInput(v.ImageMediaId!.Value, 0, null)))
            .DistinctBy(m => m.MediaAssetId).Select((m, index) => m with { SortOrder = index }).ToList();
        var states = configuration.Variants.Select(v => new VariantEditorState(resolved[v.Id].Id, v.Price, v.CompareAtPrice, v.IsActive, v.ImageMediaId)).ToArray();
        await ApplyContent(product, type, content with { Media = media }, ct, states, resolved[configuration.DefaultVariantId].Id);

        if (product.FulfillmentKind == ProductFulfillmentKind.Physical)
        {
            var location = await MainLocation(ct);
            var ids = resolved.Values.Select(v => v.Id).Order().ToArray();
            foreach (var id in ids)
                await db.InventoryItems.Where(i => i.VariantId == id && i.LocationId == location.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(i => i.Version, i => i.Version), ct);
            var balances = await db.InventoryItems.Where(i => ids.Contains(i.VariantId) && i.LocationId == location.Id).ToDictionaryAsync(i => i.VariantId, ct);
            foreach (var input in configuration.Variants)
            {
                var variant = resolved[input.Id];
                var exists = balances.TryGetValue(variant.Id, out var balance);
                if (!exists) { balance = new InventoryItem(variant.Id, location.Id); db.InventoryItems.Add(balance); }
                var target = input.Quantity ?? balance!.OnHand;
                if (target < balance!.Reserved || target < 0) throw new DomainException("Số lượng không được thấp hơn số đang giữ cho đơn hàng.");
                var wasTracking = original.ContainsKey(input.Id) ? db.Entry(variant).Property(v => v.TrackInventory).OriginalValue : true;
                if (target != balance.OnHand || wasTracking != input.TrackInventory)
                {
                    if (!await users.HasPermissionAsync(actor.GetUserId().ToString(), "Permissions.Inventory.Adjust", ct))
                        throw new ForbiddenException("Inventory.Adjust is required.");
                    if (exists && input.Quantity.HasValue) Version(input.InventoryVersion, balance.Version);
                    if (target != balance.OnHand)
                    {
                        if (!input.InventoryOperationId.HasValue || input.InventoryOperationId.Value == Guid.Empty)
                            throw new DomainException("Inventory operation ID is required.");
                        db.StockMovements.Add(balance.Adjust(input.InventoryOperationId.Value, target - balance.OnHand, "Cập nhật từ trình sửa sản phẩm", actor.GetUserId()));
                    }
                }
            }
        }
        product.Validate(type);
    }

    public Task<bool> DeleteProductAsync(Guid id, string version, CancellationToken ct) => work.ExecuteAsync(async () =>
    {
        var (product, _) = await LockedProduct(id, version, ct);
        product.Delete();
        return true;
    }, ct);
}
