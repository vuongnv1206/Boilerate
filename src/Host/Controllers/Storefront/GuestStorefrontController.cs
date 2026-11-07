using System.Net.Mail;
using System.Security.Cryptography;
using System.Text.Json;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Ordering.Shared;
using Boilerate.Application.Payments;
using Boilerate.Application.Shipping;
using Boilerate.Application.Storefront.Access;
using Boilerate.Application.Storefront.Cart;
using Boilerate.Application.Storefront.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Boilerate.Host.Controllers.Storefront;

[AllowAnonymous, Route("api/storefront"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class GuestStorefrontController(IDataProtectionProvider protection) : BaseApiController
{
    private const string CartCookieName = "boilerate_guest_cart";
    private const string EmailCookieName = "boilerate_guest_access";
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(30);

    [HttpGet("cart")]
    public async Task<CartDto> GetCart(CancellationToken ct)
    {
        Guid id = GetCartId() ?? NewCartId();
        SetProtectedCookie(CartCookieName, "Boilerate.GuestStorefront.CartCookie.v1", new CookieData(id.ToString("N"), DateTime.UtcNow.Add(SessionLifetime)));
        return await Mediator.Send(new GetGuestCartRequest(id), ct);
    }

    [HttpPut("cart")]
    public async Task<CartDto> SaveCart(SaveCart request, CancellationToken ct)
    {
        Guid id = GetCartId() ?? NewCartId();
        SetProtectedCookie(CartCookieName, "Boilerate.GuestStorefront.CartCookie.v1", new CookieData(id.ToString("N"), DateTime.UtcNow.Add(SessionLifetime)));
        return await Mediator.Send(new SaveGuestCartRequest(id, request), ct);
    }

    [HttpPut("cart/discount")]
    public async Task<CartDto> Discount(SetCartDiscount request, CancellationToken ct)
    {
        Guid id = GetCartId() ?? NewCartId();
        SetProtectedCookie(CartCookieName, "Boilerate.GuestStorefront.CartCookie.v1", new CookieData(id.ToString("N"), DateTime.UtcNow.Add(SessionLifetime)));
        return await Mediator.Send(new SetGuestCartDiscountRequest(id, request), ct);
    }

    [HttpPost("access/request-code")]
    [EnableRateLimiting("authentication")]
    public async Task<IActionResult> RequestCode(GuestAccessRequest request, CancellationToken ct)
    {
        await Mediator.Send(new RequestGuestEmailCodeRequest(request.Email), ct);
        return Accepted(new { message = "Nếu email hợp lệ, mã xác minh sẽ được gửi đến hộp thư của bạn." });
    }

    [HttpPost("access/verify-code")]
    [EnableRateLimiting("authentication")]
    public async Task<GuestAccessResult> VerifyCode(GuestAccessVerification request, CancellationToken ct)
    {
        string email = await Mediator.Send(new VerifyGuestEmailCodeRequest(request.Email, request.Code), ct);
        SetProtectedCookie(EmailCookieName, "Boilerate.GuestStorefront.EmailAccessCookie.v1", new CookieData(email, DateTime.UtcNow.Add(SessionLifetime)));
        return new GuestAccessResult(email);
    }

    [HttpGet("access/me")]
    public GuestAccessResult VerifiedAccess() => new(GetVerifiedEmail());

    [HttpPost("checkout")]
    public Task<OrderDto> Checkout(Checkout request, CancellationToken ct) =>
        Mediator.Send(
            new CheckoutGuestCartRequest(
                GetCartId() ?? throw new UnauthorizedException("Guest cart session is required."),
                GetVerifiedEmail(),
                request),
            ct);

    [HttpGet("orders")]
    public Task<Page<OrderSummary>> Orders(CancellationToken ct, int page = 1, int size = 20) =>
        Mediator.Send(new GetGuestOrdersRequest(GetVerifiedEmail(), page, size), ct);

    [HttpGet("orders/{id:guid}")]
    public Task<OrderDto> GetOrder(Guid id, CancellationToken ct) =>
        Mediator.Send(new GetGuestOrderDetailRequest(GetVerifiedEmail(), id), ct);

    [HttpPost("orders/{id:guid}/cancel")]
    public Task<OrderDto> Cancel(Guid id, CancelOrder request, CancellationToken ct) =>
        Mediator.Send(new CancelGuestOrderRequest(GetVerifiedEmail(), id, request), ct);

    [HttpGet("orders/{id:guid}/shipments")]
    public Task<List<ShipmentDto>> Shipments(Guid id, CancellationToken ct) =>
        Mediator.Send(new GetGuestOrderShipmentsRequest(id, GetVerifiedEmail()), ct);

    [HttpGet("orders/{id:guid}/digital-deliveries")]
    public Task<List<DigitalDeliveryDto>> DigitalDeliveries(Guid id, CancellationToken ct) =>
        Mediator.Send(new GetGuestDigitalDeliveriesRequest(id, GetVerifiedEmail()), ct);

    private static Guid NewCartId() => Guid.NewGuid();

    private Guid? GetCartId()
    {
        var value = ReadCookie(CartCookieName, "Boilerate.GuestStorefront.CartCookie.v1");
        return Guid.TryParseExact(value?.Value, "N", out Guid id) && id != Guid.Empty ? id : null;
    }

    private string GetVerifiedEmail()
    {
        var data = ReadCookie(EmailCookieName, "Boilerate.GuestStorefront.EmailAccessCookie.v1");
        if (data is null || !MailAddress.TryCreate(data.Value, out var address) || address.Address.ToUpperInvariant() != data.Value)
        {
            throw new UnauthorizedException("Email verification is required.");
        }

        return data.Value;
    }

    private CookieData? ReadCookie(string name, string purpose)
    {
        if (!Request.Cookies.TryGetValue(name, out string? encoded))
        {
            return null;
        }

        try
        {
            string json = protection.CreateProtector(purpose).Unprotect(encoded);
            var data = JsonSerializer.Deserialize<CookieData>(json);
            return data is not null && data.ExpiresUtc > DateTime.UtcNow ? data : null;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    private void SetProtectedCookie(string name, string purpose, CookieData data)
    {
        string protectedValue = protection.CreateProtector(purpose).Protect(JsonSerializer.Serialize(data));
        Response.Cookies.Append(name, protectedValue, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = data.ExpiresUtc,
            MaxAge = SessionLifetime,
            IsEssential = true,
        });
    }

    private sealed record CookieData(string Value, DateTime ExpiresUtc);
}

