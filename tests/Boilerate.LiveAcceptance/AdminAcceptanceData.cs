using System.Security.Cryptography;
using System.Text;
using Boilerate.Application.Catalog;
using Boilerate.Application.Identity.Roles;
using Boilerate.Application.Ordering;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Identity;
using Boilerate.Domain.Ordering;
using Boilerate.Domain.Renewals;
using Boilerate.Infrastructure.Persistence.Context;
using Boilerate.Tests;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.LiveAcceptance;

internal static class AdminAcceptanceData
{
    internal static async Task<AdminAcceptanceManifest> SeedAsync(FoundationDatabase fixture, Guid adminActorId,
        string runPassword, CancellationToken ct = default)
    {
        if (adminActorId == Guid.Empty || string.IsNullOrWhiteSpace(runPassword) || runPassword.Length < 9)
            throw new ArgumentException("Supply an existing administrator and a valid one-run password.");

        var runKey = fixture.Name["boilerate_test_".Length..];
        var actors = new Dictionary<string, AcceptanceActor>(StringComparer.Ordinal);
        foreach (var (profile, permissions) in Profiles())
        {
            using var scope = fixture.Scope(adminActorId);
            var roleService = scope.ServiceProvider.GetRequiredService<IRoleService>();
            var roleName = $"Live-{profile}-{runKey}";
            var roleId = await roleService.CreateOrUpdateAsync(new() { Name = roleName, Description = "Isolated admin acceptance profile" });
            var role = await roleService.GetByIdAsync(roleId);
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var pairs = await db.ActionInFunctions.AsNoTracking()
                .Where(pair => pair.IsActive && pair.Action.IsActive && pair.Function.IsActive)
                .Select(pair => new { pair.FunctionId, pair.ActionId, Function = pair.Function.Code, Action = pair.Action.Code })
                .ToListAsync(ct);
            var grants = permissions.Select(permission =>
            {
                var pair = pairs.SingleOrDefault(candidate => $"{candidate.Function}.{candidate.Action}" == permission)
                    ?? throw new InvalidOperationException($"The capability {permission} required by {profile} is missing.");
                return new PermissionRequest { FunctionId = pair.FunctionId, ActionId = pair.ActionId };
            }).ToList();
            await roleService.UpdatePermissionsAsync(new() { RoleId = roleId, Version = role.Version, Permissions = grants }, ct);
            actors.Add(profile, await CreateActorAsync(scope.ServiceProvider, profile, roleName, runKey, runPassword));
        }
        foreach (var customerKey in new[] { "customerA", "customerB" })
        {
            using var scope = fixture.Scope(adminActorId);
            actors.Add(customerKey, await CreateActorAsync(scope.ServiceProvider, customerKey, "Basic", runKey, runPassword));
        }

        ProductDto extension;
        ProductDto unique;
        ProductDto shared;
        var uniqueContents = new[] { $"LIVE-UNIQUE-{runKey}-ONE", $"LIVE-UNIQUE-{runKey}-TWO" };
        var sharedContent = $"https://acceptance.example.test/{runKey}/shared-content";
        using (var scope = fixture.Scope(adminActorId))
        {
            var catalog = scope.ServiceProvider.GetRequiredService<ICommerceService>();
            await catalog.InitializeAsync(ct);
            var type = await catalog.CreateTypeAsync(new($"LIVE-{runKey}", "Live digital acceptance", []), ct);
            extension = await ProductAsync(catalog, type, runKey, "extension", ProductFulfillmentKind.ManualExtension, ct);
            unique = await ProductAsync(catalog, type, runKey, "unique", ProductFulfillmentKind.DigitalUnique, ct);
            shared = await ProductAsync(catalog, type, runKey, "shared", ProductFulfillmentKind.DigitalShared, ct);
            await catalog.ImportDigitalAsync(new(unique.Id, unique.Variants.Single().Id, unique.Version,
                uniqueContents.Select((content, index) => new DigitalStockInput($"LIVE-{runKey}-{index + 1}", content)).ToList()), ct);
            await catalog.SaveSharedAsync(new(shared.Id, shared.Variants.Single().Id, shared.Version, sharedContent), ct);
        }

        var scenarios = new Dictionary<string, AcceptanceScenario>(StringComparer.Ordinal);
        foreach (var project in new[] { "desktop-light", "desktop-dark", "tablet-light", "tablet-dark" })
        {
            var key = $"renewalReady-{project}";
            scenarios.Add(key, await RenewalAsync(fixture, adminActorId, actors["customerA"], "customerA", extension,
                key, $"renewal-{runKey}-{project}@example.test", ct));
        }
        scenarios.Add("renewalReady", scenarios["renewalReady-desktop-light"]);
        scenarios.Add("renewalOtherOwner", await RenewalAsync(fixture, adminActorId, actors["customerB"], "customerB", extension,
            "renewalOtherOwner", $"renewal-{runKey}-other-owner@example.test", ct));
        scenarios.Add("digitalUniquePaid", await DigitalAsync(fixture, adminActorId, actors["customerA"], "customerA", unique,
            "digitalUniquePaid", 2, uniqueContents, ct));
        scenarios.Add("digitalSharedPaid", await DigitalAsync(fixture, adminActorId, actors["customerB"], "customerB", shared,
            "digitalSharedPaid", 2, [sharedContent], ct));
        return new(actors, scenarios);
    }

    private static Dictionary<string, string[]> Profiles() => new(StringComparer.Ordinal)
    {
        ["viewer"] = ["Product.View", "ProductType.View", "Category.View", "Inventory.View", "DigitalInventory.View",
            "Order.View", "Shipping.View", "Renewal.View", "AfterSales.View"],
        ["catalogEditor"] = ["Product.View", "Product.Create", "Product.Update", "Product.Publish", "Product.Delete",
            "ProductType.View", "ProductType.Create", "ProductType.Update", "Category.View", "Category.Create", "Category.Update", "Category.Delete", "Inventory.View"],
        ["operations"] = ["Product.View", "ProductType.View", "Category.View", "Inventory.View", "Order.View", "Order.Cancel", "Order.Fulfill",
            "Payment.Confirm", "Shipping.View", "Shipping.Dispatch", "Shipping.Deliver", "AfterSales.View", "Refund.Confirm", "Return.Receive", "Renewal.View"],
        ["renewalProcessor"] = ["Order.View", "Renewal.View", "Renewal.Process", "Renewal.Reveal"],
        ["renewalVerifier"] = ["Order.View", "Renewal.View", "Renewal.Verify"],
        ["financialViewer"] = ["FinancialSummary.View"],
    };

    private static async Task<AcceptanceActor> CreateActorAsync(IServiceProvider services, string profile, string role,
        string runKey, string password)
    {
        var email = $"live-{profile.ToLowerInvariant()}-{runKey}@example.test";
        var user = new ApplicationUser
        {
            UserName = email, Email = email, EmailConfirmed = true, IsActive = true, FirstName = "Live", LastName = profile,
        };
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        Check(await users.CreateAsync(user, password));
        Check(await users.AddToRoleAsync(user, role));
        return new(Guid.Parse(user.Id), email, password, role);
    }

    private static async Task<ProductDto> ProductAsync(ICommerceService catalog, ProductType type, string runKey,
        string kind, ProductFulfillmentKind fulfillment, CancellationToken ct)
    {
        var slug = $"live-{kind}-{runKey}";
        var product = await catalog.CreateAsync(new(type.Id, type.Version, new($"Live {kind}", slug, null, [], [], []), [],
            [new($"LIVE-{kind.ToUpperInvariant()}-{runKey}", 100000, null, [])], fulfillment), ct);
        return await catalog.StatusAsync(new(product.Id, product.Version, ProductStatus.Published), ct);
    }

    private static async Task<OrderDto> PaidOrderAsync(FoundationDatabase fixture, Guid adminActorId, AcceptanceActor customer,
        ProductDto product, string key, int quantity, CancellationToken ct)
    {
        OrderDto order;
        using (var scope = fixture.Scope(customer.UserId))
        {
            var ordering = scope.ServiceProvider.GetRequiredService<IOrderingService>();
            var current = await ordering.GetCartAsync(ct);
            var cart = await ordering.SaveCartAsync(new(current.Version, [new(product.Variants.Single().Id, quantity)]), ct);
            order = await ordering.CheckoutAsync(new(cart.Version!, Operation(fixture, key, "checkout"), OrderPaymentMethod.Prepaid,
                "Live acceptance customer", customer.Email, "0900000000", null, cart.Total), ct);
        }
        using (var scope = fixture.Scope(adminActorId))
        {
            return await scope.ServiceProvider.GetRequiredService<IPaymentService>().ConfirmPaymentAsync(order.Id,
                new(order.Version, Operation(fixture, key, "payment"), $"LIVE-{key}-{fixture.Name}", order.Total, order.Currency,
                    DateTime.UtcNow.AddSeconds(-1)), ct);
        }
    }

    private static async Task<AcceptanceScenario> RenewalAsync(FoundationDatabase fixture, Guid adminActorId, AcceptanceActor customer,
        string customerKey, ProductDto product, string key, string target, CancellationToken ct)
    {
        var order = await PaidOrderAsync(fixture, adminActorId, customer, product, key, 1, ct);
        using var scope = fixture.Scope(customer.UserId);
        var renewals = scope.ServiceProvider.GetRequiredService<IRenewalService>();
        var renewal = (await renewals.RenewalsAsync(order.Id, ct)).Single();
        renewal = await renewals.SetRenewalTargetAsync(order.Id, renewal.Id,
            new(renewal.Version, Operation(fixture, key, "target"), target), ct);
        if (order.PaymentStatus != OrderPaymentStatus.Paid || renewal.Status != RenewalStatus.Ready)
            throw new InvalidOperationException("The renewal acceptance scenario is not ready for processing.");
        return new(order.Id, renewal.Id, product.Id, product.Variants.Single().Id, customer.UserId, customerKey, target);
    }

    private static async Task<AcceptanceScenario> DigitalAsync(FoundationDatabase fixture, Guid adminActorId, AcceptanceActor customer,
        string customerKey, ProductDto product, string key, int quantity, IReadOnlyList<string> expected, CancellationToken ct)
    {
        var order = await PaidOrderAsync(fixture, adminActorId, customer, product, key, quantity, ct);
        using var scope = fixture.Scope(customer.UserId);
        var delivered = await scope.ServiceProvider.GetRequiredService<IPaymentService>().DigitalDeliveriesAsync(order.Id, ct);
        if (!expected.Order(StringComparer.Ordinal).SequenceEqual(delivered.Select(item => item.Content).Order(StringComparer.Ordinal)))
            throw new InvalidOperationException("The digital acceptance scenario did not deliver the expected fixture content.");
        return new(order.Id, null, product.Id, product.Variants.Single().Id, customer.UserId, customerKey, ExpectedDigitalContents: expected);
    }

    private static Guid Operation(FoundationDatabase fixture, string scenario, string action) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes($"{fixture.Name}:{scenario}:{action}"))[..16]);

    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
    }
}

internal sealed record AdminAcceptanceManifest(Dictionary<string, AcceptanceActor> Actors, Dictionary<string, AcceptanceScenario> Scenarios);
internal sealed record AcceptanceActor(Guid UserId, string Email, string Password, string Role);
internal sealed record AcceptanceScenario(Guid OrderId, Guid? RenewalId, Guid ProductId, Guid VariantId, Guid CustomerId,
    string CustomerKey, string? TargetAccountIdentifier = null, IReadOnlyList<string>? ExpectedDigitalContents = null);
