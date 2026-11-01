using System.Text.Json;
using Boilerate.Application.Catalog;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Ordering;
using Boilerate.Infrastructure.Commerce;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;

namespace Boilerate.Host.Controllers.Catalog;

[AllowAnonymous, Route("api/storefront"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class GuestStorefrontController(IGuestOrderingService ordering, IGuestAccessService access,
    IShippingService shipping, IPaymentService payments, IDataProtectionProvider protection) : BaseApiController
{
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(30);
    private const string CartCookieName = "boilerate_guest_cart";
    private const string EmailCookieName = "boilerate_guest_access";

    [HttpGet("cart")]
    public async Task<CartDto> GetCart(CancellationToken ct)
    {
        var id = GetCartId() ?? NewCartId();
        SetProtectedCookie(CartCookieName, "Boilerate.GuestStorefront.CartCookie.v1", new CookieData(id.ToString("N"), DateTime.UtcNow.Add(SessionLifetime)));
        return await ordering.GetGuestCartAsync(id, ct);
    }

    [HttpPut("cart")]
    public async Task<CartDto> SaveCart(SaveCart request, CancellationToken ct)
    {
        var id = GetCartId() ?? NewCartId();
        SetProtectedCookie(CartCookieName, "Boilerate.GuestStorefront.CartCookie.v1", new CookieData(id.ToString("N"), DateTime.UtcNow.Add(SessionLifetime)));
        return await ordering.SaveGuestCartAsync(id, request, ct);
    }

    [HttpPut("cart/discount")]
    public async Task<CartDto> Discount(SetCartDiscount request, CancellationToken ct)
    {
        var id = GetCartId() ?? NewCartId();
        SetProtectedCookie(CartCookieName, "Boilerate.GuestStorefront.CartCookie.v1", new CookieData(id.ToString("N"), DateTime.UtcNow.Add(SessionLifetime)));
        return await ordering.SetGuestDiscountAsync(id, request, ct);
    }

    [HttpPost("access/request-code")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("authentication")]
    public async Task<IActionResult> RequestCode(GuestAccessRequest request, CancellationToken ct)
    {
        await access.RequestCodeAsync(request.Email, ct);
        return Accepted(new { message = "Nếu email hợp lệ, mã xác minh sẽ được gửi đến hộp thư của bạn." });
    }

    [HttpPost("access/verify-code")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("authentication")]
    public async Task<GuestAccessResult> VerifyCode(GuestAccessVerification request, CancellationToken ct)
    {
        await access.VerifyCodeAsync(request.Email, request.Code, ct);
        var email = OrderingService.NormalizeEmail(request.Email);
        SetProtectedCookie(EmailCookieName, "Boilerate.GuestStorefront.EmailAccessCookie.v1", new CookieData(email, DateTime.UtcNow.Add(SessionLifetime)));
        return new(email);
    }

    [HttpGet("access/me")]
    public GuestAccessResult VerifiedAccess() => new(GetVerifiedEmail());

    [HttpPost("checkout")]
    public Task<OrderDto> Checkout(Checkout request, CancellationToken ct) =>
        ordering.CheckoutGuestAsync(GetCartId() ?? throw new UnauthorizedException("Guest cart session is required."), GetVerifiedEmail(), request, ct);

    [HttpGet("orders")]
    public Task<Page<OrderSummary>> Orders(CancellationToken ct, int page = 1, int size = 20) =>
        ordering.GuestOrdersAsync(GetVerifiedEmail(), page, size, ct);

    [HttpGet("orders/{id:guid}")]
    public Task<OrderDto> GetOrder(Guid id, CancellationToken ct) => ordering.GetGuestOrderAsync(GetVerifiedEmail(), id, ct);

    [HttpPost("orders/{id:guid}/cancel")]
    public Task<OrderDto> Cancel(Guid id, CancelOrder request, CancellationToken ct) =>
        ordering.CancelGuestOrderAsync(GetVerifiedEmail(), id, request, ct);

    [HttpGet("orders/{id:guid}/shipments")]
    public Task<List<Boilerate.Application.Ordering.ShipmentDto>> Shipments(Guid id, CancellationToken ct) =>
        shipping.GuestShipmentsAsync(id, GetVerifiedEmail(), ct);

    [HttpGet("orders/{id:guid}/digital-deliveries")]
    public Task<List<DigitalDeliveryDto>> DigitalDeliveries(Guid id, CancellationToken ct) =>
        payments.GuestDigitalDeliveriesAsync(id, GetVerifiedEmail(), ct);

    private Guid? GetCartId()
    {
        var value = ReadCookie(CartCookieName, "Boilerate.GuestStorefront.CartCookie.v1");
        return Guid.TryParseExact(value?.Value, "N", out var id) && id != Guid.Empty ? id : null;
    }

    private string GetVerifiedEmail()
    {
        var data = ReadCookie(EmailCookieName, "Boilerate.GuestStorefront.EmailAccessCookie.v1");
        if (data is null || !System.Net.Mail.MailAddress.TryCreate(data.Value, out var address) || address.Address.ToUpperInvariant() != data.Value)
            throw new UnauthorizedException("Email verification is required.");
        return data.Value;
    }

    private CookieData? ReadCookie(string name, string purpose)
    {
        if (!Request.Cookies.TryGetValue(name, out var encoded)) return null;
        try
        {
            var json = protection.CreateProtector(purpose).Unprotect(encoded);
            var data = JsonSerializer.Deserialize<CookieData>(json);
            return data is not null && data.ExpiresUtc > DateTime.UtcNow ? data : null;
        }
        catch (System.Security.Cryptography.CryptographicException) { return null; }
    }

    private void SetProtectedCookie(string name, string purpose, CookieData data)
    {
        var protectedValue = protection.CreateProtector(purpose).Protect(JsonSerializer.Serialize(data));
        Response.Cookies.Append(name, protectedValue, new CookieOptions
        {
            HttpOnly = true, Secure = Request.IsHttps, SameSite = SameSiteMode.Lax, Path = "/",
            Expires = data.ExpiresUtc, MaxAge = SessionLifetime, IsEssential = true
        });
    }

    private static Guid NewCartId() => Guid.NewGuid();
    private sealed record CookieData(string Value, DateTime ExpiresUtc);
}

[AllowAnonymous, Route("api/storefront/settings"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PublicStorefrontSettingsController(IStorefrontSettingsService settings) : BaseApiController
{
    [HttpGet]
    public Task<GuestPaymentSettingsDto> Get(CancellationToken ct) => settings.GetPublicAsync(ct);
}
