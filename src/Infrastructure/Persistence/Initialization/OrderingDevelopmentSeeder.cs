using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Boilerate.Application.Catalog;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Identity.Users;
using Boilerate.Application.Ordering;
using Boilerate.Domain.Identity;
using Boilerate.Domain.Ordering;
using Boilerate.Infrastructure.Auth;
using Boilerate.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Boilerate.Infrastructure.Persistence.Initialization;

/// <summary>Creates replay-safe order lifecycle fixtures through commerce use cases.</summary>
public sealed class OrderingDevelopmentSeeder(
    ApplicationDbContext db,
    IOrderingService ordering,
    IPaymentService payments,
    IShippingService shipping,
    UserManager<ApplicationUser> users,
    IUserService permissions,
    ICurrentUser currentUser,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<OrderingDevelopmentSeeder> logger) : ICustomSeeder
{
    private static readonly string[] RequiredPermissions =
    [
        "Permissions.Order.View", "Permissions.Order.Cancel", "Permissions.Order.Fulfill",
        "Permissions.Payment.Confirm", "Permissions.Shipping.Dispatch", "Permissions.Shipping.Deliver"
    ];

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (!environment.IsDevelopment())
            throw new InvalidOperationException("Ordering development data cannot be seeded outside Development.");
        cancellationToken.ThrowIfCancellationRequested();
        var data = await OrderingSeedData.ReadAsync(cancellationToken);
        await InitializeActorAsync(cancellationToken);
        var skus = data.Scenarios.SelectMany(s => s.Lines).Select(line => line.Sku.ToUpperInvariant()).Distinct().ToArray();
        var variants = await db.ProductVariants.AsNoTracking().Where(variant => skus.Contains(variant.NormalizedSku))
            .ToDictionaryAsync(variant => variant.NormalizedSku, cancellationToken);
        var created = 0;

        foreach (var scenario in data.Scenarios)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var operationId = StableId($"ordering-development-v1:checkout:{scenario.Key}");
            if (await db.Orders.AnyAsync(order => order.OperationId == operationId, cancellationToken))
            {
                logger.LogInformation("Preserved existing development order scenario {ScenarioKey}", scenario.Key);
                continue;
            }

            var lineInputs = scenario.Lines.Select(line => new CartLineInput(variants[Normalize(line.Sku)].Id, line.Quantity)).ToList();
            var cart = await ordering.GetCartAsync(cancellationToken);
            cart = await ordering.SaveCartAsync(new(cart.Version, lineInputs), cancellationToken);
            if (!cart.CanCheckout || cart.Version is null)
                throw new InvalidOperationException($"Catalog data cannot create order scenario {scenario.Key}; the cart is not checkout-ready.");

            var order = await ordering.CheckoutAsync(new(cart.Version, operationId, scenario.PaymentMethod,
                "Boilerate Development", "orders-development@example.test", "0900000000",
                "Development address, Ho Chi Minh City", cart.Total), cancellationToken);
            order = await ApplyScenarioAsync(scenario, order, cancellationToken);
            created++;
            logger.LogInformation("Created development order {OrderNumber} for scenario {ScenarioKey} ({Status})",
                order.Number, scenario.Key, order.Status);
        }

        logger.LogInformation("Ordering development data: {Created} orders created, {Skipped} existing orders preserved",
            created, data.Scenarios.Length - created);
    }

    private async Task<OrderDto> ApplyScenarioAsync(OrderScenarioSeed scenario, OrderDto order, CancellationToken ct)
    {
        return scenario.Action switch
        {
            "None" => order,
            "Cancel" => await ordering.CancelAsync(order.Id, new(order.Version), true, ct),
            "ConfirmAndReview" => await ConfirmAndReviewAsync(order, ct),
            "CompleteCod" => await CompleteCodAsync(order, scenario, ct),
            "Expire" => await ExpireAsync(order, ct),
            _ => throw new InvalidOperationException($"Unknown ordering seed action {scenario.Action}.")
        };
    }

    private async Task<OrderDto> ConfirmAndReviewAsync(OrderDto order, CancellationToken ct)
    {
        var paid = await payments.ConfirmPaymentAsync(order.Id, new(order.Version, StableId($"ordering-development-v1:payment:{order.Number}"),
            $"DEV-{order.Number}", order.Total, order.Currency, DateTime.UtcNow.AddMinutes(-1)), ct);
        // A paid order requiring operator attention is a valid lifecycle state. The domain method
        // remains the only writer of the fulfillment state; the seeder only arranges the fixture.
        var tracked = await db.Orders.SingleAsync(item => item.Id == paid.Id, ct);
        tracked.MarkFulfillmentReview();
        await db.SaveChangesAsync(ct);
        return paid with { Version = tracked.Version, FulfillmentStatus = OrderFulfillmentStatus.NeedsReview };
    }

    private async Task<OrderDto> CompleteCodAsync(OrderDto order, OrderScenarioSeed scenario, CancellationToken ct)
    {
        var dispatch = await shipping.DispatchShipmentAsync(order.Id, new(order.Version,
            StableId($"ordering-development-v1:dispatch:{scenario.Key}"), "Development Carrier", $"DEV-{order.Number}",
            order.Items.Where(item => item.FulfillmentKind == Domain.Catalog.ProductFulfillmentKind.Physical)
                .Select(item => new ShipmentLineInput(item.Id, item.Quantity)).ToList()), ct);
        var delivered = await shipping.DeliverShipmentAsync(order.Id, dispatch.Id,
            new(dispatch.OrderVersion, dispatch.Version, StableId($"ordering-development-v1:deliver:{scenario.Key}")), ct);
        return await payments.ConfirmPaymentAsync(order.Id, new(delivered.OrderVersion,
            StableId($"ordering-development-v1:cod-payment:{scenario.Key}"), $"COD-{order.Number}", order.Total,
            order.Currency, DateTime.UtcNow.AddMinutes(-1), "ManualCOD"), ct);
    }

    private async Task<OrderDto> ExpireAsync(OrderDto order, CancellationToken ct)
    {
        // Order.Expire is still the state transition. The private expiry timestamp is only moved
        // backwards in this Development fixture so the normal expiration use case can be replayed.
        var tracked = await db.Orders.SingleAsync(item => item.Id == order.Id, ct);
        db.Entry(tracked).Property(nameof(Order.ExpiresAt)).CurrentValue = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync(ct);
        await ordering.ExpireAsync(ct);
        return await ordering.GetOrderAsync(order.Id, true, ct);
    }

    private async Task InitializeActorAsync(CancellationToken ct)
    {
        var email = configuration["DevelopmentSeed:ActorEmail"];
        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException("Set DevelopmentSeed:ActorEmail to an existing active, authorized operator before running --seed-dev.");
        var user = await users.FindByEmailAsync(email.Trim());
        if (user is not { IsActive: true } || !Guid.TryParse(user.Id, out var actorId) || actorId == Guid.Empty)
            throw new InvalidOperationException("The development seed actor must be an existing active user.");
        foreach (var permission in RequiredPermissions)
            if (!await permissions.HasPermissionAsync(user.Id, permission, ct))
                throw new ForbiddenException($"The development seed actor requires {permission}; no grants were changed.");
        if (currentUser.GetUserId() == Guid.Empty)
            ((ICurrentUserInitializer)currentUser).SetCurrentUser(new ClaimsPrincipal(new ClaimsIdentity(
                [new(ClaimTypes.NameIdentifier, user.Id), new(ClaimTypes.Email, user.Email!)], "DevelopmentSeeder")));
        else if (currentUser.GetUserId() != actorId)
            throw new InvalidOperationException("The seed actor differs from the current scope identity.");
    }

    private static string Normalize(string sku) => sku.Trim().ToUpperInvariant();
    private static Guid StableId(string key) => new(SHA256.HashData(Encoding.UTF8.GetBytes(key))[..16]);
}
