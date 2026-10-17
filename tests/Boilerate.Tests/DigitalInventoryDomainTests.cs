using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using Boilerate.Domain.DigitalInventory;
using Xunit;
namespace Boilerate.Tests;

public sealed class DigitalInventoryDomainTests
{
    [Fact]
    public void ExistingProductConstructorDefaultsToPhysicalAndDeliveryKindSurvivesEditing()
    {
        var physical = new Product(Guid.NewGuid(), "Shirt", "shirt");
        Assert.Equal(ProductFulfillmentKind.Physical, physical.FulfillmentKind);
        var digital = new Product(Guid.NewGuid(), "Course", "course", fulfillmentKind: ProductFulfillmentKind.DigitalUnique);
        digital.Edit("Updated course", "updated-course", "New presentation");
        Assert.Equal(ProductFulfillmentKind.DigitalUnique, digital.FulfillmentKind);
        Assert.Throws<DomainException>(() => new Product(Guid.NewGuid(), "Bad", "bad", fulfillmentKind: (ProductFulfillmentKind)0));
    }

    [Theory]
    [InlineData(ProductFulfillmentKind.Physical)]
    [InlineData(ProductFulfillmentKind.ManualExtension)]
    [InlineData((ProductFulfillmentKind)99)]
    public void DigitalStockRejectsModesWithoutStoredDigitalContent(ProductFulfillmentKind kind)
    {
        Assert.Throws<DomainException>(() => NewItem(kind));
    }

    [Fact]
    public void DigitalStockRequiresExplicitNonemptyIdentifiersAndNormalizesReference()
    {
        var id = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var item = new DigitalStockItem(id, productId, variantId, ProductFulfillmentKind.DigitalUnique, "  batch-a.01  ", "ciphertext");
        Assert.Equal(id, item.Id);
        Assert.Equal(productId, item.ProductId);
        Assert.Equal(variantId, item.VariantId);
        Assert.Equal("BATCH-A.01", item.Reference);
        Assert.Equal(DigitalStockState.Available, item.State);
        Assert.Throws<DomainException>(() => new DigitalStockItem(Guid.Empty, productId, variantId, ProductFulfillmentKind.DigitalUnique, "A", "ciphertext"));
        Assert.Throws<DomainException>(() => new DigitalStockItem(id, Guid.Empty, variantId, ProductFulfillmentKind.DigitalUnique, "A", "ciphertext"));
        Assert.Throws<DomainException>(() => new DigitalStockItem(id, productId, Guid.Empty, ProductFulfillmentKind.DigitalUnique, "A", "ciphertext"));
        Assert.Throws<DomainException>(() => new DigitalStockItem(id, productId, variantId, ProductFulfillmentKind.DigitalUnique, "user@example.com", "ciphertext"));
    }

    [Fact]
    public void SharedRequiresReservedReferenceAndCanReplaceContentWithoutChangingOwnership()
    {
        var item = NewItem(ProductFulfillmentKind.DigitalShared, " shared ");
        var version = item.Version;
        item.ReplaceSharedContent("replacement-ciphertext");
        Assert.Equal("SHARED", item.Reference);
        Assert.Equal("replacement-ciphertext", item.ProtectedContent);
        Assert.NotEqual(version, item.Version);
        Assert.Throws<DomainException>(() => NewItem(ProductFulfillmentKind.DigitalShared, "OTHER"));
    }

    [Fact]
    public void UniqueContentCannotBeReplacedAndFailureDoesNotChangeVersionOrCiphertext()
    {
        var item = NewItem();
        var version = item.Version;
        Assert.Throws<DomainException>(() => item.ReplaceSharedContent("new-ciphertext"));
        Assert.Equal("ciphertext", item.ProtectedContent);
        Assert.Equal(version, item.Version);
    }

    [Fact]
    public void AvailabilityChangesVersionAndKeepsContentForReactivation()
    {
        var item = NewItem();
        var version = item.Version;
        item.SetAvailability(false);
        Assert.Equal(DigitalStockState.Disabled, item.State);
        Assert.NotEqual(version, item.Version);
        version = item.Version;
        item.SetAvailability(true);
        Assert.Equal(DigitalStockState.Available, item.State);
        Assert.Equal("ciphertext", item.ProtectedContent);
        Assert.NotEqual(version, item.Version);
    }

    [Fact]
    public void PlaintextValidationPreservesFormattingAndLimitsUtf8Bytes()
    {
        var content = "  tài khoản\r\npassword with spaces  ";
        Assert.Same(content, DigitalStockItem.ValidateContent(content));
        Assert.Equal(8000, DigitalStockItem.ValidateContent(new string('a', 8000)).Length);
        Assert.Equal(4000, DigitalStockItem.ValidateContent(new string('é', 4000)).Length);
        Assert.Throws<DomainException>(() => DigitalStockItem.ValidateContent(new string('é', 4001)));
        Assert.Throws<DomainException>(() => DigitalStockItem.ValidateContent(new string('a', 8001)));
        Assert.Throws<DomainException>(() => DigitalStockItem.ValidateContent(" \r\n\t"));
        Assert.Throws<DomainException>(() => DigitalStockItem.ValidateContent(null!));
    }

    [Fact]
    public void CiphertextValidationProtectsPersistenceBoundsAndFailedReplacementKeepsContent()
    {
        var item = NewItem(ProductFulfillmentKind.DigitalShared, "SHARED");
        item.ReplaceSharedContent(new string('x', 16000));
        var version = item.Version;
        Assert.Throws<DomainException>(() => item.ReplaceSharedContent(new string('x', 16001)));
        Assert.Throws<DomainException>(() => item.ReplaceSharedContent("  "));
        Assert.Equal(16000, item.ProtectedContent.Length);
        Assert.Equal(version, item.Version);
        Assert.Throws<DomainException>(() => new DigitalStockItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ProductFulfillmentKind.DigitalUnique, "A", null!));
    }

    private static DigitalStockItem NewItem(ProductFulfillmentKind kind = ProductFulfillmentKind.DigitalUnique, string reference = "CODE-01") =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), kind, reference, "ciphertext");
}
