using Boilerate.Application.Catalog.Products;
using Boilerate.Application.Catalog.ProductTypes;
using Boilerate.Application.Inventory.Digital;
using Boilerate.Application.Inventory.Physical;
using Boilerate.Domain.Catalog;
using Boilerate.Tests;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace Boilerate.LiveAcceptance;

internal static class GuestStorefrontAcceptanceData
{
    internal static async Task<StorefrontAcceptanceScenario> SeedAsync(FoundationDatabase fixture, Guid adminActorId,
        CancellationToken ct = default)
    {
        var runKey = fixture.Name["boilerate_test_".Length..];
        using var scope = fixture.Scope(adminActorId);
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(new InitializeInventoryRequest(), ct);
        var type = await sender.Send(new CreateProductTypeRequest($"GUEST-{runKey}", "Guest storefront acceptance", []), ct);
        var physical = await PublishedAsync(sender, type, runKey, "physical", ProductFulfillmentKind.Physical, ct);
        var physicalVariantId = physical.Variants.Single().Id;
        var balance = (await sender.Send(new GetInventoryItemsRequest(1, 100, null, physicalVariantId), ct)).Items.Single();
        await sender.Send(new AdjustInventoryRequest(balance.Id, balance.Version, Guid.NewGuid(), 20, "Live guest storefront stock"), ct);

        var digital = await PublishedAsync(sender, type, runKey, "digital", ProductFulfillmentKind.DigitalUnique, ct);
        var digitalVariantId = digital.Variants.Single().Id;
        var digitalContent = $"LIVE-GUEST-DELIVERY-{runKey}";
        await sender.Send(new ImportDigitalStockRequest(digital.Id, digitalVariantId, digital.Version,
            // Desktop, tablet and mobile in both themes, with two spare codes for isolated replay checks.
            Enumerable.Range(1, 8).Select(index => new DigitalStockInput($"GUEST-{runKey}-{index}", digitalContent)).ToList()), ct);

        var settings = await sender.Send(new Boilerate.Application.Storefront.Settings.GetAdminStorefrontSettingsRequest(), ct);
        await sender.Send(new Boilerate.Application.Storefront.Settings.UpdateStorefrontSettingsRequest("Ngân hàng kiểm thử", "0123456789", "BOILERATE ACCEPTANCE",
            "Chi nhánh kiểm thử", "Ghi mã đơn trong nội dung chuyển khoản.", true, settings.Version), ct);

        return new(physical.Id, physical.Slug, physical.Name, physicalVariantId,
            digital.Id, digital.Slug, digital.Name, digitalVariantId, digitalContent,
            "Ngân hàng kiểm thử", "0123456789", "BOILERATE ACCEPTANCE");
    }

    private static async Task<ProductDto> PublishedAsync(ISender sender, ProductType type, string runKey,
        string suffix, ProductFulfillmentKind kind, CancellationToken ct)
    {
        var slug = $"guest-{suffix}-{runKey}";
        var sku = $"GUEST-{suffix.ToUpperInvariant()}-{runKey}";
        var draft = await sender.Send(new CreateProductRequest(type.Id, type.Version,
            new($"Live guest {suffix} product", slug, "Sản phẩm kiểm thử storefront với nội dung tiếng Việt dài để rà bố cục và đọc bàn phím.", [], [], []),
            [], [new(sku, 123000, null, new())], kind), ct);
        return await sender.Send(new ChangeProductStatusRequest(draft.Id, draft.Version, ProductStatus.Published), ct);
    }
}

internal sealed record StorefrontAcceptanceScenario(Guid PhysicalProductId, string PhysicalSlug, string PhysicalName,
    Guid PhysicalVariantId, Guid DigitalProductId, string DigitalSlug, string DigitalName, Guid DigitalVariantId,
    string DigitalContent, string BankName, string AccountNumber, string AccountHolder);

internal sealed record AcceptanceMailSnapshot(IReadOnlyList<string> To, string Subject, string? Body);

internal sealed class AcceptanceMailSink : Boilerate.Application.Common.Mailing.IMailService
{
    private readonly ConcurrentQueue<AcceptanceMailSnapshot> _messages = new();

    public Task SendAsync(Boilerate.Application.Common.Mailing.MailRequest request, CancellationToken ct)
    {
        _messages.Enqueue(new(request.To.ToArray(), request.Subject, request.Body));
        return Task.CompletedTask;
    }

    public IReadOnlyList<AcceptanceMailSnapshot> Snapshot() => _messages.ToArray();
}
