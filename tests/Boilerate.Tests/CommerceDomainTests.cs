using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using Boilerate.Domain.Inventory;
using Xunit;
namespace Boilerate.Tests;

public sealed class CommerceDomainTests
{
    [Fact]
    public void OptionsIdentifySkusWhileCategoriesAndLabelsDoNot()
    {
        var p = new Product(Guid.NewGuid(), "T-shirt", "t-shirt");
        var color = p.AddOption("color", "Color", 3);
        var black = color.AddValue("black", "Black");
        var size = p.AddOption("size", "Size", 3);
        var medium = size.AddValue("m", "Medium");
        var v = p.AddVariant("SHIRT-B-M", 100000, 120000, new Dictionary<Guid, Guid> { [size.Id] = medium.Id, [color.Id] = black.Id }, 200);
        var key = v.CombinationKey;
        black.Rename("Đen", 4);
        p.SetCategories(new[] { Guid.NewGuid(), Guid.NewGuid() });
        Assert.Equal(key, v.CombinationKey);
        Assert.Equal(2, p.Categories.Count);
        Assert.Throws<DomainException>(() => p.AddVariant("SECOND", 100000, null, new Dictionary<Guid, Guid> { [color.Id] = black.Id, [size.Id] = medium.Id }, 200));
        Assert.Throws<DomainException>(() => p.AddVariant("MISSING", 100000, null, new Dictionary<Guid, Guid>(), 200));
        var material = p.AddOption("material", "Material", 3);
        var cotton = material.AddValue("cotton", "Cotton");
        p.AssignOptionValue(v.Id, material.Id, cotton.Id);
        Assert.Equal(3, v.Selections.Count);
        Assert.Contains(v.Selections, selection => selection.OptionId == material.Id && selection.OptionValueId == cotton.Id);
    }

    [Fact]
    public void ExistingSkusCanReceiveValuesForANewOptionAxis()
    {
        var p = new Product(Guid.NewGuid(), "T-shirt", "t-shirt");
        var color = p.AddOption("color", "Color", 3);
        var red = color.AddValue("red", "Red");
        var blue = color.AddValue("blue", "Blue");
        var first = p.AddVariant("SHIRT-RED", 100, null, new Dictionary<Guid, Guid> { [color.Id] = red.Id }, 200);
        var second = p.AddVariant("SHIRT-BLUE", 100, null, new Dictionary<Guid, Guid> { [color.Id] = blue.Id }, 200);

        var size = p.AddOption("size", "Size", 3);
        var small = size.AddValue("s", "Small");
        var medium = size.AddValue("m", "Medium");
        p.AssignOptionValue(first.Id, size.Id, small.Id);
        p.AssignOptionValue(second.Id, size.Id, small.Id);
        var added = p.AddVariant("SHIRT-RED-M", 100, null,
            new Dictionary<Guid, Guid> { [color.Id] = red.Id, [size.Id] = medium.Id }, 200);

        Assert.Equal(2, first.Selections.Count);
        Assert.Equal(small.Id, first.Selections.Single(selection => selection.OptionId == size.Id).OptionValueId);
        Assert.Equal(small.Id, second.Selections.Single(selection => selection.OptionId == size.Id).OptionValueId);
        Assert.Contains(added.Selections, selection => selection.OptionId == size.Id && selection.OptionValueId == medium.Id);
        Assert.Equal(3, p.Variants.Count);
    }
    [Fact]
    public void PublicationRequiresWholeVndPricesAndRequiredTypedFields()
    {
        var type = new ProductType("CLOTHES", "Clothes");
        var field = type.AddField("material", ProductFieldType.Text, "Material", null, true, true, false);
        var p = new Product(type.Id, "T-shirt", "t-shirt");
        var v = p.AddVariant("SHIRT", null, null, new Dictionary<Guid, Guid>(), 200);
        Assert.Throws<DomainException>(() => p.ChangeStatus(ProductStatus.Published, type));
        p.EditVariant(v.Id, 100000, null, true, null);
        Assert.Throws<DomainException>(() => p.ChangeStatus(ProductStatus.Published, type));
        p.SetFields(type, new[] { new ProductFieldValue(p.Id, type.Id, field.Id, ProductFieldType.Text, text: "Cotton") });
        p.ChangeStatus(ProductStatus.Published, type);
        Assert.Equal(ProductStatus.Published, p.Status);
        Assert.Throws<DomainException>(() => p.EditVariant(v.Id, 1.5m, null, true, null));
        Assert.Throws<DomainException>(() => p.EditVariant(v.Id, 100, 50, true, null));
        Assert.Throws<DomainException>(() => new ProductFieldValue(p.Id, type.Id, field.Id, ProductFieldType.Text, text: "Cotton", number: 1));
        Assert.Throws<DomainException>(() => type.AddField("unsafe", ProductFieldType.Text, "Unsafe", null, true, true, true));
    }
    [Fact]
    public void StockCannotGoNegativeAndCategoryCannotContainItself()
    {
        var item = new InventoryItem(Guid.NewGuid(), Guid.NewGuid());
        var movement = item.Adjust(Guid.NewGuid(), 3, "Initial", Guid.NewGuid());
        Assert.Equal(3, movement.QuantityAfter);
        Assert.Throws<DomainException>(() => item.Adjust(Guid.NewGuid(), -4, "Too much", Guid.Empty));
        Assert.Equal(3, item.OnHand);
        Assert.Throws<DomainException>(() => new ProductFieldValue(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),ProductFieldType.Number,number:decimal.MinValue));
        var category = new Category("A", null, "a");
        Assert.Throws<DomainException>(() => category.Edit("A", "a", null, Guid.NewGuid(), 0, true, new[] { category.Id }));
    }
}
