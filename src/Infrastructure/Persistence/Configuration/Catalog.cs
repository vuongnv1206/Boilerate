using Boilerate.Domain.Catalog;
using Boilerate.Domain.DigitalInventory;
using Boilerate.Domain.Inventory;
using Boilerate.Domain.Media;
using Boilerate.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
namespace Boilerate.Infrastructure.Persistence.Configuration;

internal static class CommerceModel
{
    internal static void Configure(ModelBuilder m, bool postgres)
    {
        var audited = new[] { typeof(Product), typeof(Category), typeof(ProductType), typeof(InventoryLocation), typeof(InventoryItem), typeof(MediaAsset), typeof(DigitalStockItem) };
        foreach (var type in audited)
        {
            var b = m.Entity(type);
            b.HasBaseType((Type?)null);
            b.HasKey("Id");
            b.Property("Version").HasMaxLength(32).IsConcurrencyToken();
        }
        foreach (var type in audited.Concat(new[] { typeof(ProductVariant), typeof(ProductOption), typeof(ProductOptionValue), typeof(ProductFieldDefinition), typeof(ProductFieldChoice), typeof(StockMovement) }))
            m.Entity(type).Property<Guid>("Id").ValueGeneratedNever();
        var p = m.Entity<Product>();
        p.ToTable("Products", "Catalog", t =>
        {
            t.HasCheckConstraint("CK_Product_Status", "\"Status\" IN (1,2,3)");
            t.HasCheckConstraint("CK_Product_FulfillmentKind", "\"FulfillmentKind\" IN (1,2,3,4)");
        });
        p.Property(x => x.FulfillmentKind).HasDefaultValue(ProductFulfillmentKind.Physical);
        p.Property(x => x.Name).HasMaxLength(256);
        p.Property(x => x.Description).HasMaxLength(20000);
        p.Property(x => x.Slug).HasMaxLength(200);
        p.Property(x => x.NormalizedSlug).HasMaxLength(200);
        p.HasIndex(x => x.NormalizedSlug).IsUnique();
        p.HasIndex(x => new { x.Status, x.ProductTypeId });
        p.HasAlternateKey(x => new { x.Id, x.ProductTypeId });
        p.HasAlternateKey(x => new { x.Id, x.FulfillmentKind });
        p.HasOne<ProductType>().WithMany().HasForeignKey(x => x.ProductTypeId).OnDelete(DeleteBehavior.Restrict);
        var c = m.Entity<Category>();
        c.ToTable("Categories", "Catalog", t => t.HasCheckConstraint("CK_Category_Parent", "\"ParentId\" IS NULL OR \"ParentId\" <> \"Id\""));
        c.Property(x => x.Name).HasMaxLength(256);
        c.Property(x => x.Description).HasMaxLength(4000);
        c.Property(x => x.Slug).HasMaxLength(200);
        c.Property(x => x.NormalizedSlug).HasMaxLength(200);
        c.HasIndex(x => x.NormalizedSlug).IsUnique();
        c.HasOne<Category>().WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        var pc = m.Entity<ProductCategory>();
        pc.ToTable("ProductCategories", "Catalog");
        pc.HasKey(x => new { x.ProductId, x.CategoryId });
        pc.HasOne<Product>().WithMany(x => x.Categories).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        pc.HasOne<Category>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        pc.HasIndex(x => new { x.CategoryId, x.ProductId });
        var v = m.Entity<ProductVariant>();
        v.ToTable("ProductVariants", "Catalog", t =>
        {
            t.HasCheckConstraint("CK_Variant_Price", "(\"Price\" IS NULL OR (\"Price\" >= 0 AND \"Price\" = FLOOR(\"Price\"))) AND (\"CompareAtPrice\" IS NULL OR (\"Price\" IS NOT NULL AND \"CompareAtPrice\" > \"Price\" AND \"CompareAtPrice\" = FLOOR(\"CompareAtPrice\"))) AND \"Currency\" = 'VND'");
        });
        v.HasKey(x => x.Id);
        v.HasAlternateKey(x => new { x.ProductId, x.Id });
        v.Property(x => x.Sku).HasMaxLength(100);
        v.Property(x => x.NormalizedSku).HasMaxLength(100);
        v.Property(x => x.Currency).HasMaxLength(3);
        // Max options is capped at 10 in configuration; the persisted key must fit SQL Server's index limit.
        v.Property(x => x.CombinationKey).HasMaxLength(700).IsUnicode(false);
        v.Property(x => x.Price).HasPrecision(18, 2);
        v.Property(x => x.CompareAtPrice).HasPrecision(18, 2);
        v.HasIndex(x => x.NormalizedSku).IsUnique();
        v.HasIndex(x => new { x.ProductId, x.CombinationKey }).IsUnique();
        v.HasIndex(x => x.ProductId).IsUnique().HasFilter(postgres ? "\"IsDefault\" = TRUE" : "[IsDefault] = 1");
        v.HasOne<Product>().WithMany(x => x.Variants).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        v.HasOne<ProductMedia>().WithMany().HasForeignKey(x => new { x.ProductId, x.ImageMediaId }).HasPrincipalKey(x => new { x.ProductId, x.MediaAssetId }).OnDelete(DeleteBehavior.Restrict);
        var o = m.Entity<ProductOption>();
        o.ToTable("ProductOptions", "Catalog");
        o.HasKey(x => x.Id);
        o.HasAlternateKey(x => new { x.ProductId, x.Id });
        o.Property(x => x.Code).HasMaxLength(100);
        o.Property(x => x.Name).HasMaxLength(100);
        o.HasIndex(x => new { x.ProductId, x.Code }).IsUnique();
        o.HasOne<Product>().WithMany(x => x.Options).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        var ov = m.Entity<ProductOptionValue>();
        ov.ToTable("ProductOptionValues", "Catalog");
        ov.HasKey(x => x.Id);
        ov.HasAlternateKey(x => new { x.OptionId, x.Id });
        ov.Property(x => x.Code).HasMaxLength(100);
        ov.Property(x => x.Label).HasMaxLength(100);
        ov.HasIndex(x => new { x.OptionId, x.Code }).IsUnique();
        ov.HasOne<ProductOption>().WithMany(x => x.Values).HasForeignKey(x => x.OptionId).OnDelete(DeleteBehavior.Restrict);
        var vo = m.Entity<VariantOptionValue>();
        vo.ToTable("VariantOptionValues", "Catalog");
        vo.HasKey(x => new { x.VariantId, x.OptionId });
        vo.HasOne<ProductVariant>().WithMany(x => x.Selections).HasForeignKey(x => new { x.ProductId, x.VariantId }).HasPrincipalKey(x => new { x.ProductId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        vo.HasOne<ProductOption>().WithMany().HasForeignKey(x => new { x.ProductId, x.OptionId }).HasPrincipalKey(x => new { x.ProductId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        vo.HasOne<ProductOptionValue>().WithMany().HasForeignKey(x => new { x.OptionId, x.OptionValueId }).HasPrincipalKey(x => new { x.OptionId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        var pt = m.Entity<ProductType>();
        pt.ToTable("ProductTypes", "Catalog");
        pt.Property(x => x.Code).HasMaxLength(100);
        pt.Property(x => x.NormalizedCode).HasMaxLength(100);
        pt.Property(x => x.Name).HasMaxLength(256);
        pt.HasIndex(x => x.NormalizedCode).IsUnique();
        var f = m.Entity<ProductFieldDefinition>();
        f.ToTable("ProductFieldDefinitions", "Catalog", t => t.HasCheckConstraint("CK_Field_Type", "\"DataType\" BETWEEN 1 AND 5"));
        f.HasKey(x => x.Id);
        f.HasAlternateKey(x => new { x.Id, x.ProductTypeId, x.DataType });
        f.Property(x => x.Code).HasMaxLength(100);
        f.Property(x => x.Label).HasMaxLength(256);
        f.Property(x => x.HelpText).HasMaxLength(1000);
        f.HasIndex(x => new { x.ProductTypeId, x.Code }).IsUnique();
        f.HasOne<ProductType>().WithMany(x => x.Fields).HasForeignKey(x => x.ProductTypeId).OnDelete(DeleteBehavior.Restrict);
        var fc = m.Entity<ProductFieldChoice>();
        fc.ToTable("ProductFieldChoices", "Catalog");
        fc.HasKey(x => x.Id);
        fc.HasAlternateKey(x => new { x.FieldDefinitionId, x.Id });
        fc.Property(x => x.Code).HasMaxLength(100);
        fc.Property(x => x.Label).HasMaxLength(256);
        fc.HasIndex(x => new { x.FieldDefinitionId, x.Code }).IsUnique();
        fc.HasOne<ProductFieldDefinition>().WithMany(x => x.Choices).HasForeignKey(x => x.FieldDefinitionId).OnDelete(DeleteBehavior.Restrict);
        var fv = m.Entity<ProductFieldValue>();
        fv.ToTable("ProductFieldValues", "Catalog", t =>
        {
            var columns = new[] { "TextValue", "NumberValue", "BooleanValue", "DateValue", "ChoiceId" };
            var branches = columns.Select((column, index) => "(\"FieldType\" = " + (index + 1) + " AND " + string.Join(" AND ", columns.Select(other => "\"" + other + "\" IS " + (other == column ? "NOT NULL" : "NULL"))) + ")");
            t.HasCheckConstraint("CK_FieldValue_Type", string.Join(" OR ", branches));
        });
        fv.HasKey(x => new { x.ProductId, x.FieldDefinitionId });
        fv.Property(x => x.TextValue).HasMaxLength(500);
        fv.Property(x => x.NumberValue).HasPrecision(18, 4);
        fv.HasOne<Product>().WithMany(x => x.FieldValues).HasForeignKey(x => new { x.ProductId, x.ProductTypeId }).HasPrincipalKey(x => new { x.Id, x.ProductTypeId }).OnDelete(DeleteBehavior.Restrict);
        fv.HasOne<ProductFieldDefinition>().WithMany().HasForeignKey(x => new { x.FieldDefinitionId, x.ProductTypeId, x.FieldType }).HasPrincipalKey(x => new { x.Id, x.ProductTypeId, x.DataType }).OnDelete(DeleteBehavior.Restrict);
        fv.HasOne<ProductFieldChoice>().WithMany().HasForeignKey(x => new { x.FieldDefinitionId, x.ChoiceId }).HasPrincipalKey(x => new { x.FieldDefinitionId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        foreach (string column in new[] { "TextValue", "NumberValue", "BooleanValue", "DateValue", "ChoiceId" })
            fv.HasIndex("FieldDefinitionId", column, "ProductId");
        var media = m.Entity<MediaAsset>();
        media.ToTable("MediaAssets", "Media");
        media.Property(x => x.StorageKey).HasMaxLength(300);
        media.HasIndex(x => x.StorageKey).IsUnique();
        media.Property(x => x.ContentType).HasMaxLength(50);
        media.HasIndex(x => new { x.State, x.ExpiresAt });
        var pm = m.Entity<ProductMedia>();
        pm.ToTable("ProductMedia", "Catalog");
        pm.HasKey(x => new { x.ProductId, x.MediaAssetId });
        pm.Property(x => x.AltText).HasMaxLength(500);
        pm.HasOne<Product>().WithMany(x => x.Media).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        pm.HasOne<MediaAsset>().WithMany().HasForeignKey(x => x.MediaAssetId).OnDelete(DeleteBehavior.Restrict);
        var loc = m.Entity<InventoryLocation>();
        loc.ToTable("InventoryLocations", "Inventory");
        loc.Property(x => x.Code).HasMaxLength(100);
        loc.Property(x => x.Name).HasMaxLength(256);
        loc.HasIndex(x => x.Code).IsUnique();
        var item = m.Entity<InventoryItem>();
        item.ToTable("InventoryItems", "Inventory", t => t.HasCheckConstraint("CK_Inventory_Nonnegative", "\"OnHand\" >= 0"));
        item.HasIndex(x => new { x.VariantId, x.LocationId }).IsUnique();
        item.HasOne<ProductVariant>().WithMany().HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.Restrict);
        item.HasOne<InventoryLocation>().WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
        var move = m.Entity<StockMovement>();
        move.ToTable("StockMovements", "Inventory", t => t.HasCheckConstraint("CK_Movement_Amounts", "\"Delta\" <> 0 AND \"QuantityAfter\" >= 0"));
        move.HasKey(x => x.Id);
        move.Property(x => x.Reason).HasMaxLength(1000);
        move.HasIndex(x => x.OperationId).IsUnique();
        move.HasIndex(x => new { x.InventoryItemId, x.CreatedAt, x.Id });
        move.HasOne<InventoryItem>().WithMany().HasForeignKey(x => x.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
        var digital = m.Entity<DigitalStockItem>();
        digital.ToTable("DigitalStockItems", "DigitalInventory", t =>
        {
            t.HasCheckConstraint("CK_DigitalStockItem_FulfillmentKind", "\"FulfillmentKind\" IN (2,3)");
            t.HasCheckConstraint("CK_DigitalStockItem_State", "\"State\" IN (1,2)");
        });
        digital.Property(x => x.Reference).HasMaxLength(100).IsUnicode(false).IsRequired();
        digital.Property(x => x.ProtectedContent).HasMaxLength(16000).IsRequired();
        digital.HasIndex(x => new { x.VariantId, x.Reference }).IsUnique();
        digital.HasIndex(x => x.VariantId).IsUnique().HasFilter(postgres ? "\"FulfillmentKind\" = 3" : "[FulfillmentKind] = 3");
        digital.HasIndex(x => new { x.ProductId, x.VariantId, x.State });
        digital.HasOne<ProductVariant>().WithMany().HasForeignKey(x => new { x.ProductId, x.VariantId }).HasPrincipalKey(x => new { x.ProductId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        digital.HasOne<Product>().WithMany().HasForeignKey(x => new { x.ProductId, x.FulfillmentKind }).HasPrincipalKey(x => new { x.Id, x.FulfillmentKind }).OnDelete(DeleteBehavior.Restrict);
        var msg = m.Entity<OutboxMessage>();
        msg.ToTable("OutboxMessages", "Messaging");
        msg.HasKey(x => x.MessageId);
        msg.Property(x => x.Type).HasMaxLength(100);
        msg.Property(x => x.CorrelationId).HasMaxLength(200);
        msg.Property(x => x.LastError).HasMaxLength(2000);
        msg.HasIndex(x => new { x.State, x.NextAttemptAt, x.LeaseUntil });
        var gate = m.Entity<CommerceLock>();
        gate.ToTable("CommerceLocks", "Catalog");
        gate.HasKey(x => x.Name);
        gate.Property(x => x.Name).HasMaxLength(50);
        gate.Property(x => x.Version).HasMaxLength(32).IsConcurrencyToken();
        gate.HasData(new CommerceLock { Name = "categories", Version = "initial" }, new CommerceLock { Name = "initialization", Version = "initial" });
    }
}
