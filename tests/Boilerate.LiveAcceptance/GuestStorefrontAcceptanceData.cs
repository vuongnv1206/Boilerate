using Boilerate.Application.Catalog;
using Boilerate.Application.Ordering;
using Boilerate.Domain.Catalog;
using Boilerate.Tests;
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
        var catalog = scope.ServiceProvider.GetRequiredService<ICommerceService>();
        await catalog.InitializeAsync(ct);
        var type = await catalog.CreateTypeAsync(new($"GUEST-{runKey}", "Guest storefront acceptance", []), ct);
        var physical = await PublishedAsync(catalog, type, runKey, "physical", ProductFulfillmentKind.Physical, ct);
        var physicalVariantId = physical.Variants.Single().Id;
        var balance = (await catalog.InventoryAsync(1, 100, ct, variantId: physicalVariantId)).Items.Single();
        await catalog.AdjustAsync(new(balance.Id, balance.Version, Guid.NewGuid(), 20, "Live guest storefront stock"), ct);

        var digital = await PublishedAsync(catalog, type, runKey, "digital", ProductFulfillmentKind.DigitalUnique, ct);
        var digitalVariantId = digital.Variants.Single().Id;
        var digitalContent = $"LIVE-GUEST-DELIVERY-{runKey}";
        await catalog.ImportDigitalAsync(new(digital.Id, digitalVariantId, digital.Version,
            // Desktop, tablet and mobile in both themes, with two spare codes for isolated replay checks.
            Enumerable.Range(1, 8).Select(index => new DigitalStockInput($"GUEST-{runKey}-{index}", digitalContent)).ToList()), ct);

        var settingsService = scope.ServiceProvider.GetRequiredService<IStorefrontSettingsService>();
        var settings = await settingsService.GetAdminAsync(ct);
        await settingsService.UpdateAsync(new("Ngân hàng kiểm thử", "0123456789", "BOILERATE ACCEPTANCE",
            "Chi nhánh kiểm thử", "Ghi mã đơn trong nội dung chuyển khoản.", true, settings.Version), ct);

        return new(physical.Id, physical.Slug, physical.Name, physicalVariantId,
            digital.Id, digital.Slug, digital.Name, digitalVariantId, digitalContent,
            "Ngân hàng kiểm thử", "0123456789", "BOILERATE ACCEPTANCE");
    }

    private static async Task<ProductDto> PublishedAsync(ICommerceService catalog, ProductType type, string runKey,
        string suffix, ProductFulfillmentKind kind, CancellationToken ct)
    {
        var slug = $"guest-{suffix}-{runKey}";
        var sku = $"GUEST-{suffix.ToUpperInvariant()}-{runKey}";
        var draft = await catalog.CreateAsync(new(type.Id, type.Version,
            new($"Live guest {suffix} product", slug, "Sản phẩm kiểm thử storefront với nội dung tiếng Việt dài để rà bố cục và đọc bàn phím.", [], [], []),
            [], [new(sku, 123000, null, new())], kind), ct);
        return await catalog.StatusAsync(new(draft.Id, draft.Version, ProductStatus.Published), ct);
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
