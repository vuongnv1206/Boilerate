using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Boilerate.Application.Catalog.Products;
using Boilerate.Application.Inventory.Digital;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Identity.Tokens;
using Boilerate.Application.Ordering.Shared;
using Boilerate.Application.Payments;
using Boilerate.Application.Shipping;
using Boilerate.Application.Storefront.Access;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using Boilerate.Domain.DigitalInventory;
using Boilerate.Domain.Messaging;
using Boilerate.Domain.Ordering;
using Boilerate.Infrastructure.Commerce;
using Boilerate.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Boilerate.Tests;

[Collection("Foundation")]
public sealed class GuestStorefrontIntegrationTests
{
    private static CatalogTestClient Catalog(IServiceScope scope) => CatalogTestClient.ForScope(scope);

    private static OrderingTestClient Ordering(IServiceScope scope, TimeProvider? clock = null) =>
        OrderingTestClient.ForScope(scope, clock: clock);

    private static CreateProductRequest Product(ProductType type, string slug,
        ProductFulfillmentKind kind = ProductFulfillmentKind.Physical) =>
        new(type.Id, type.Version, new("Product " + slug, slug, null, [], [], []), [], [new(slug, 100, null, new())], kind);

    private static async Task<ProductDto> Published(CatalogTestClient catalog, ProductType type, string slug,
        ProductFulfillmentKind kind = ProductFulfillmentKind.Physical)
    {
        var product = await catalog.CreateAsync(Product(type, slug, kind), default);
        return await catalog.StatusAsync(new(product.Id, product.Version, ProductStatus.Published), default);
    }

    private static async Task Receive(CatalogTestClient catalog, Guid variantId, int quantity)
    {
        var balance = (await catalog.InventoryAsync(1, 100, default)).Items.Single(i => i.VariantId == variantId);
        await catalog.AdjustAsync(new(balance.Id, balance.Version, Guid.NewGuid(), quantity, "Guest storefront test"), default);
    }

    private static Checkout Checkout(CartDto cart, OrderPaymentMethod method, string email, Guid? operationId = null) =>
        new(cart.Version!, operationId ?? Guid.NewGuid(), method, "Guest Customer", email, "0901234567",
            "123 Test Street", cart.Total);

    private static string ReadCode(ApplicationDbContext db, IDataProtectionProvider protection)
    {
        var payload = db.OutboxMessages.AsNoTracking().Where(m => m.Type == "GuestEmailVerification")
            .OrderByDescending(m => m.OccurredAt).Select(m => m.Payload).First();
        var json = protection.CreateProtector("Boilerate.GuestStorefront.EmailOtp.Outbox.v1").Unprotect(payload);
        return JsonSerializer.Deserialize<GuestEmailVerificationMessage>(json)!.Code;
    }

    [Fact, Trait("Category", "ProviderIntegration")]
    public async Task AnonymousCartAndEmailAccessUseProtectedHttpOnlyCookies()
    {
        await using var fixture = new FoundationDatabase("mssql", true);
        await fixture.CreateAsync();
        Guid variantId;
        using (var scope = fixture.Scope(Guid.NewGuid()))
        {
            var catalog = Catalog(scope);
            await catalog.InitializeAsync(default);
            var type = await catalog.CreateTypeAsync(new("GUESTCART", "Guest cart", []), default);
            var product = await Published(catalog, type, "guest-cart-shirt");
            variantId = product.Variants.Single().Id;
            await Receive(catalog, variantId, 2);
        }

        await using var app = await TestHostApplication.StartAsync(fixture, true);
        using var client = app.GetTestClient();
        var response = await client.GetAsync("/api/storefront/cart");
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var cartCookie = Assert.Single(response.Headers.GetValues("Set-Cookie").Where(value => value.StartsWith("boilerate_guest_cart=", StringComparison.Ordinal)));
        Assert.Contains("httponly", cartCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cartCookie, StringComparison.OrdinalIgnoreCase);
        var cookiePair = cartCookie.Split(';', 2)[0];
        client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", cookiePair);

        response = await client.PutAsJsonAsync("/api/storefront/cart", new SaveCart(null, [new(variantId, 1)]));
        response.EnsureSuccessStatusCode();
        var savedCart = (await response.Content.ReadFromJsonAsync<CartDto>())!;
        Assert.True(savedCart.CanCheckout);
        Assert.Equal(ProductFulfillmentKind.Physical, Assert.Single(savedCart.Items).FulfillmentKind);
        Assert.False(savedCart.HasDigitalItems);
        response = await client.GetAsync("/api/storefront/cart");
        Assert.Equal(savedCart.Id, (await response.Content.ReadFromJsonAsync<CartDto>())!.Id);

        using var isolatedClient = app.GetTestClient();
        var isolatedCart = (await (await isolatedClient.GetAsync("/api/storefront/cart")).Content.ReadFromJsonAsync<CartDto>())!;
        Assert.Empty(isolatedCart.Items);
        Assert.NotEqual(savedCart.Id, isolatedCart.Id);
        Assert.Equal(HttpStatusCode.Unauthorized, (await isolatedClient.GetAsync("/api/storefront/access/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await isolatedClient.GetAsync("/api/storefront/orders")).StatusCode);

        response = await client.PostAsJsonAsync("/api/storefront/access/request-code", new GuestAccessRequest("guest@example.test"));
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        using var scopeForOtp = fixture.Scope();
        var db = scopeForOtp.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var protection = scopeForOtp.ServiceProvider.GetRequiredService<IDataProtectionProvider>();
        var code = ReadCode(db, protection);
        var challenge = await db.Set<GuestEmailChallenge>().SingleAsync();
        Assert.NotEqual(code, challenge.CodeHash);
        Assert.DoesNotContain(code, (await db.OutboxMessages.SingleAsync(m => m.Type == "GuestEmailVerification")).Payload, StringComparison.Ordinal);

        response = await client.PostAsJsonAsync("/api/storefront/access/verify-code", new GuestAccessVerification("guest@example.test", code));
        response.EnsureSuccessStatusCode();
        var accessCookie = Assert.Single(response.Headers.GetValues("Set-Cookie").Where(value => value.StartsWith("boilerate_guest_access=", StringComparison.Ordinal)));
        Assert.Contains("httponly", accessCookie, StringComparison.OrdinalIgnoreCase);
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", cookiePair + "; " + accessCookie.Split(';', 2)[0]);
        var access = await client.GetFromJsonAsync<GuestAccessResult>("/api/storefront/access/me");
        Assert.Equal("GUEST@EXAMPLE.TEST", access!.Email);
        Assert.Equal(0, (await client.GetFromJsonAsync<Page<OrderSummary>>("/api/storefront/orders"))!.Total);
    }

    [Fact, Trait("Category", "ProviderIntegration")]
    public async Task GuestCheckoutUsesServerQuoteAndOperationReplayAndScopesOrdersToVerifiedEmail()
    {
        await using var fixture = new FoundationDatabase("mssql", true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var catalog = Catalog(scope);
        var ordering = Ordering(scope);
        await catalog.InitializeAsync(default);
        var type = await catalog.CreateTypeAsync(new("GUESTORDER", "Guest order", []), default);
        var physical = await Published(catalog, type, "guest-order-shirt");
        var physicalId = physical.Variants.Single().Id;
        await Receive(catalog, physicalId, 2);
        var guestId = Guid.NewGuid();
        var cart = await ordering.SaveGuestCartAsync(guestId, new(null, [new(physicalId, 1)]), default);
        Assert.True(cart.CanCheckout);
        Assert.Empty((await ordering.GetGuestCartAsync(Guid.NewGuid(), default)).Items);

        var operationId = Guid.NewGuid();
        var request = Checkout(cart, OrderPaymentMethod.CashOnDelivery, "guest@example.test", operationId);
        await Assert.ThrowsAsync<ForbiddenException>(() => ordering.CheckoutGuestAsync(guestId, "guest@example.test", request with { Email = "other@example.test" }, default));
        await catalog.EditVariantAsync(new(physical.Id, physicalId, physical.Version, 125, null, true, null), default);
        await Assert.ThrowsAsync<ConflictException>(() => ordering.CheckoutGuestAsync(guestId, "guest@example.test", request, default));
        Assert.Empty(await db.Orders.ToListAsync());

        cart = await ordering.GetGuestCartAsync(guestId, default);
        request = Checkout(cart, OrderPaymentMethod.CashOnDelivery, "guest@example.test", operationId);
        var order = await ordering.CheckoutGuestAsync(guestId, "guest@example.test", request, default);
        var replay = await ordering.CheckoutGuestAsync(guestId, "guest@example.test", request, default);
        Assert.Equal(order.Id, replay.Id);
        Assert.True((await db.Orders.AsNoTracking().SingleAsync()).IsGuestOrder);
        Assert.Equal("GUEST@EXAMPLE.TEST", (await db.Orders.AsNoTracking().SingleAsync()).NormalizedEmail);
        Assert.Equal(1, (await ordering.GuestOrdersAsync("guest@example.test", 1, 20, default)).Total);
        Assert.Equal(0, (await ordering.GuestOrdersAsync("other@example.test", 1, 20, default)).Total);
        Assert.Equal(order.Id, (await ordering.GetGuestOrderAsync("guest@example.test", order.Id, default)).Id);
        await Assert.ThrowsAsync<NotFoundException>(() => ordering.GetGuestOrderAsync("other@example.test", order.Id, default));
        await Assert.ThrowsAsync<NotFoundException>(() => ordering.GuestShipmentsAsync(order.Id, "other@example.test", default));
        var cancelled = await ordering.CancelGuestOrderAsync("guest@example.test", order.Id, new(order.Version), default);
        Assert.Equal(OrderStatus.Cancelled, cancelled.Status);
        Assert.Equal(0, (await db.InventoryItems.AsNoTracking().SingleAsync()).Reserved);

        var digital = await Published(catalog, type, "guest-order-extension", ProductFulfillmentKind.ManualExtension);
        var emptyCart = await ordering.GetGuestCartAsync(guestId, default);
        var digitalCart = await ordering.SaveGuestCartAsync(guestId, new(emptyCart.Version, [new(digital.Variants.Single().Id, 1)]), default);
        Assert.True(digitalCart.HasDigitalItems);
        await Assert.ThrowsAsync<DomainException>(() => ordering.CheckoutGuestAsync(guestId,
            "guest@example.test", Checkout(digitalCart, OrderPaymentMethod.CashOnDelivery, "guest@example.test"), default));
    }

    [Fact, Trait("Category", "ProviderIntegration")]
    public async Task GuestDigitalDeliveryRequiresPaidOrderAndMatchingEmail()
    {
        await using var fixture = new FoundationDatabase("mssql", true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var catalog = Catalog(scope);
        var ordering = Ordering(scope);
        await catalog.InitializeAsync(default);
        var type = await catalog.CreateTypeAsync(new("GUESTDIGITAL", "Guest digital", []), default);
        var product = await Published(catalog, type, "guest-digital-license", ProductFulfillmentKind.DigitalUnique);
        var variantId = product.Variants.Single().Id;
        await catalog.ImportDigitalAsync(new(product.Id, variantId, product.Version, [new("ONE", "GuestSecret-123")]), default);
        var sender = scope.ServiceProvider.GetRequiredService<MediatR.ISender>();
        var cart = await ordering.SaveGuestCartAsync(Guid.NewGuid(), new(null, [new(variantId, 1)]), default);
        var guestId = (await db.Carts.AsNoTracking().SingleAsync()).CustomerId;
        await Assert.ThrowsAsync<ConflictException>(() => ordering.CheckoutGuestAsync(guestId, "digital@example.test",
            Checkout(cart, OrderPaymentMethod.Prepaid, "digital@example.test"), default));
        Assert.Empty(await db.Orders.ToListAsync());
        await sender.Send(new Boilerate.Application.Storefront.Settings.UpdateStorefrontSettingsRequest("Bank", "123456", "SHOP OWNER", null, null, true, ""), default);
        cart = await ordering.GetGuestCartAsync(guestId, default);
        var order = await ordering.CheckoutGuestAsync(guestId, "digital@example.test",
            Checkout(cart, OrderPaymentMethod.Prepaid, "digital@example.test"), default);
        await Assert.ThrowsAsync<ConflictException>(() => ordering.GuestDigitalDeliveriesAsync(order.Id, "digital@example.test", default));
        await Assert.ThrowsAsync<NotFoundException>(() => ordering.GuestDigitalDeliveriesAsync(order.Id, "wrong@example.test", default));

        var paid = await ordering.ConfirmPaymentAsync(order.Id,
            new(order.Version, Guid.NewGuid(), "BANK-REF-1", order.Total, "VND", DateTime.UtcNow), default);
        Assert.Equal(OrderPaymentStatus.Paid, paid.PaymentStatus);
        var deliveries = await ordering.GuestDigitalDeliveriesAsync(order.Id, "digital@example.test", default);
        Assert.Equal("GuestSecret-123", Assert.Single(deliveries).Content);
    }

    [Fact, Trait("Category", "ProviderIntegration")]
    public async Task CompetingGuestCheckoutsCannotReserveTheLastPhysicalItemTwice()
    {
        await using var fixture = new FoundationDatabase("mssql", true);
        await fixture.CreateAsync();
        using (var scope = fixture.Scope(Guid.NewGuid()))
        {
            var catalog = Catalog(scope);
            await catalog.InitializeAsync(default);
            var type = await catalog.CreateTypeAsync(new("GUESTLAST", "Guest last item", []), default);
            var product = await Published(catalog, type, "guest-last-item-shirt");
            await Receive(catalog, product.Variants.Single().Id, 1);
        }

        Guid lastVariantId;
        using (var scope = fixture.Scope())
            lastVariantId = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().ProductVariants
                .Where(variant => variant.Sku == "guest-last-item-shirt").Select(variant => variant.Id).SingleAsync();

        async Task<bool> TryGuestOrder(Guid guestId)
        {
            using var scope = fixture.Scope();
            var ordering = Ordering(scope);
            var cart = await ordering.SaveGuestCartAsync(guestId, new(null, [new(lastVariantId, 1)]), default);
            try
            {
                await ordering.CheckoutGuestAsync(guestId, $"{guestId:N}@example.test",
                    Checkout(cart, OrderPaymentMethod.CashOnDelivery, $"{guestId:N}@example.test"), default);
                return true;
            }
            catch (ConflictException)
            {
                return false;
            }
        }

        var outcomes = await Task.WhenAll(TryGuestOrder(Guid.NewGuid()), TryGuestOrder(Guid.NewGuid()));
        Assert.Single(outcomes.Where(success => success));
        Assert.Single(outcomes.Where(success => !success));
        using var finalScope = fixture.Scope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Single(await finalDb.Orders.AsNoTracking().ToListAsync());
        Assert.Equal(1, (await finalDb.InventoryItems.AsNoTracking().SingleAsync()).Reserved);
    }

    [Fact, Trait("Category", "ProviderIntegration")]
    public async Task GuestOtpIsHashedExpiresLimitsAttemptsAndThrottlesResends()
    {
        await using var fixture = new FoundationDatabase("mssql", true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var protection = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>();
        var clock = new TestClock(new DateTimeOffset(2026, 10, 24, 10, 0, 0, TimeSpan.Zero));
        var access = GuestAccessTestClient.ForScope(scope, clock);

        await access.RequestCodeAsync("attempts@example.test", default);
        var code = ReadCode(db, protection);
        var wrongCode = code == "000000" ? "000001" : "000000";
        for (var attempt = 0; attempt < 5; attempt++)
            await Assert.ThrowsAsync<Boilerate.Application.Common.Exceptions.ForbiddenException>(
                () => access.VerifyCodeAsync("attempts@example.test", wrongCode, default));
        await Assert.ThrowsAsync<Boilerate.Application.Common.Exceptions.ForbiddenException>(
            () => access.VerifyCodeAsync("attempts@example.test", code, default));
        Assert.Equal(5, (await db.Set<GuestEmailChallenge>().SingleAsync()).Attempts);

        await access.RequestCodeAsync("expired@example.test", default);
        var expiredCode = ReadCode(db, protection);
        clock.Advance(TimeSpan.FromMinutes(10));
        await Assert.ThrowsAsync<Boilerate.Application.Common.Exceptions.ForbiddenException>(
            () => access.VerifyCodeAsync("expired@example.test", expiredCode, default));

        for (var request = 0; request < 5; request++)
        {
            await access.RequestCodeAsync("limited@example.test", default);
            if (request < 4) clock.Advance(TimeSpan.FromSeconds(61));
        }
        clock.Advance(TimeSpan.FromSeconds(61));
        await Assert.ThrowsAsync<ConflictException>(() => access.RequestCodeAsync("limited@example.test", default));
        Assert.Equal(5, await db.Set<GuestEmailChallenge>().CountAsync(c => c.NormalizedEmail == "LIMITED@EXAMPLE.TEST"));
    }

    private sealed class TestClock(DateTimeOffset initial) : TimeProvider
    {
        private DateTimeOffset _now = initial;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan amount) => _now += amount;
    }
}
