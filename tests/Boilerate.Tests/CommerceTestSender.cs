using Boilerate.Application;
using Boilerate.Application.AfterSales;
using Boilerate.Application.Catalog.Categories;
using Boilerate.Application.Catalog.Media;
using Boilerate.Application.Catalog.Products;
using Boilerate.Application.Catalog.ProductTypes;
using Boilerate.Application.Common.FileStorage;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Identity.Users;
using Boilerate.Application.Inventory.Digital;
using Boilerate.Application.Inventory.Physical;
using Boilerate.Application.Ordering.Cart;
using Boilerate.Application.Ordering.Discounts;
using Boilerate.Application.Ordering.Orders;
using Boilerate.Application.Ordering.Shared;
using Boilerate.Application.Payments;
using Boilerate.Application.Renewals;
using Boilerate.Application.Shipping;
using Boilerate.Application.Storefront.Access;
using Boilerate.Application.Storefront.Cart;
using Boilerate.Application.Storefront.Orders;
using Boilerate.Application.Storefront.PublicCatalog;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Renewals;
using Boilerate.Infrastructure.Commerce;
using Boilerate.Infrastructure.Persistence.Context;
using MediatR;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Boilerate.Tests;

public sealed record SaveCategoryTestInput(
    Guid? Id,
    string? Version,
    string Name,
    string Slug,
    string? Description,
    Guid? ParentId,
    int SortOrder,
    bool IsActive);

public static class CommerceTestSender
{
    public static ISender Create(
        IServiceScope scope,
        bool allowPermissions = true,
        string? deniedPermission = null,
        ApplicationDbContext? dbOverride = null,
        TimeProvider? clock = null,
        CommerceSettings? commerceSettings = null,
        OrderingSettings? orderingSettings = null)
    {
        var db = dbOverride ?? scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = Substitute.For<IUserService>();
        users.HasPermissionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => allowPermissions && (string)call[1] != deniedPermission);

        var services = new ServiceCollection();
        services.AddApplication();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IApplicationDbContext>(db);
        services.AddSingleton<ICommerceUnitOfWork>(new CommerceUnitOfWork(db));
        services.AddSingleton(scope.ServiceProvider.GetRequiredService<ICommerceMediaStorage>());
        services.AddSingleton(scope.ServiceProvider.GetRequiredService<ICurrentUser>());
        services.AddSingleton(users);
        services.AddSingleton<IOptions<CommerceSettings>>(Options.Create(commerceSettings ?? new CommerceSettings()));
        services.AddSingleton<IOptions<OrderingSettings>>(Options.Create(orderingSettings ?? new OrderingSettings()));
        services.AddSingleton(clock ?? TimeProvider.System);
        services.AddSingleton(scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>());

        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }
}

public sealed class CatalogTestClient(ISender sender)
{
    public ISender Sender => sender;

    public static CatalogTestClient ForScope(
        IServiceScope scope,
        bool allowPermissions = true,
        string? deniedPermission = null,
        CommerceSettings? settings = null) =>
        new(CommerceTestSender.Create(scope, allowPermissions, deniedPermission, commerceSettings: settings));

    public Task<Guid> InitializeAsync(CancellationToken ct = default) =>
        sender.Send(new InitializeInventoryRequest(), ct);

    public Task<List<ProductType>> TypesAsync(CancellationToken ct = default) =>
        sender.Send(new GetProductTypesRequest(), ct);

    public Task<ProductType> TypeAsync(Guid id, CancellationToken ct = default) =>
        sender.Send(new GetProductTypeRequest(id), ct);

    public Task<ProductType> CreateTypeAsync(CreateProductTypeRequest request, CancellationToken ct = default) =>
        sender.Send(request, ct);

    public Task<ProductType> EditTypeAsync(UpdateProductTypeRequest request, CancellationToken ct = default) =>
        sender.Send(request, ct);

    public Task<List<Category>> CategoriesAsync(bool publicOnly, CancellationToken ct = default) =>
        sender.Send(new GetCategoriesRequest(publicOnly), ct);

    public Task<List<CategorySummary>> CategorySummariesAsync(CancellationToken ct = default) =>
        sender.Send(new GetCategorySummariesRequest(), ct);

    public Task<Category> CategoryAsync(SaveCategoryTestInput r, CancellationToken ct = default) =>
        r.Id.HasValue
            ? sender.Send(new UpdateCategoryRequest(r.Id, r.Version, r.Name, r.Slug, r.Description, r.ParentId, r.SortOrder, r.IsActive), ct)
            : sender.Send(new CreateCategoryRequest(r.Name, r.Slug, r.Description, r.ParentId, r.SortOrder, r.IsActive), ct);

    public Task<bool> DeleteCategoryAsync(DeleteCategoryRequest request, CancellationToken ct = default) =>
        sender.Send(request, ct);

    public Task<List<CategoryProductResult>> SetCategoryProductsAsync(SetCategoryProductsRequest request, CancellationToken ct = default) =>
        sender.Send(request, ct);

    public Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken ct = default) =>
        sender.Send(request, ct);

    public Task<ProductDto> EditAsync(UpdateProductContentRequest request, CancellationToken ct = default) =>
        sender.Send(request, ct);

    public Task<ProductDto> SaveEditorAsync(SaveProductEditorRequest request, CancellationToken ct = default) =>
        sender.Send(request, ct);

    public Task<bool> DeleteProductAsync(Guid id, string version, CancellationToken ct = default) =>
        sender.Send(new DeleteProductRequest(id, version), ct);

    public Task<BulkProductStatusResult> BulkStatusAsync(BulkUpdateProductStatusRequest request, CancellationToken ct = default) =>
        sender.Send(request, ct);

    public Task<ProductDto> StatusAsync(ChangeProductStatusRequest request, CancellationToken ct = default) =>
        sender.Send(request, ct);

    public Task<ProductDto> AddVariantAsync(AddProductVariantRequest request, CancellationToken ct = default) =>
        sender.Send(request, ct);

    public Task<ProductDto> EditVariantAsync(UpdateProductVariantRequest request, CancellationToken ct = default) =>
        sender.Send(request, ct);

    public Task<ProductDto> DefaultAsync(SetDefaultVariantRequest request, CancellationToken ct = default) =>
        sender.Send(request, ct);

    public Task<ProductDto> OptionAsync(UpdateProductOptionRequest request, CancellationToken ct = default) =>
        sender.Send(request, ct);

    public Task<List<Dictionary<string, string>>> PreviewAsync(PreviewVariantsRequest request, CancellationToken ct = default) =>
        sender.Send(request, ct);

    public Task<ProductDto> GetAsync(Guid id, bool publishedOnly, CancellationToken ct = default) =>
        sender.Send(new GetProductRequest(id), ct);

    public Task<ProductDto> GetBySlugAsync(string slug, CancellationToken ct = default) =>
        sender.Send(new GetPublicProductBySlugRequest(slug), ct);

    public Task<Page<ProductSummary>> SearchAsync(ProductSearch filter, bool publishedOnly, CancellationToken ct = default) =>
        publishedOnly
            ? sender.Send(new SearchPublicProductsRequest(filter), ct)
            : sender.Send(new SearchProductsRequest(filter), ct);

    public Task<Page<InventoryDto>> InventoryAsync(
        int page,
        int size,
        CancellationToken ct = default,
        Guid? productId = null,
        Guid? variantId = null) =>
        sender.Send(new GetInventoryItemsRequest(page, size, productId, variantId), ct);

    public Task<MovementDto> AdjustAsync(AdjustInventoryRequest request, CancellationToken ct = default) =>
        sender.Send(request, ct);

    public Task<Page<MovementDto>> MovementsAsync(Guid itemId, int page, int size, CancellationToken ct = default) =>
        sender.Send(new GetStockMovementsRequest(itemId, page, size), ct);

    public Task<List<DigitalStockDto>> ImportDigitalAsync(ImportDigitalStockRequest request, CancellationToken ct = default) =>
        sender.Send(request, ct);

    public Task<DigitalStockDto> SaveSharedAsync(SaveSharedDigitalContentRequest request, CancellationToken ct = default) =>
        sender.Send(request, ct);

    public Task<DigitalStockDto> SetDigitalStockAvailabilityAsync(SetDigitalStockAvailabilityRequest request, CancellationToken ct = default) =>
        sender.Send(request, ct);

    public Task<Page<DigitalStockDto>> DigitalStockAsync(Guid variantId, int page, int size, CancellationToken ct = default) =>
        sender.Send(new GetDigitalStockRequest(variantId, page, size), ct);

    public Task<DigitalSecretDto> RevealDigitalStockAsync(Guid id, CancellationToken ct = default) =>
        sender.Send(new RevealDigitalStockRequest(id), ct);

    public Task<MediaDto> UploadMediaAsync(IFormFile file, CancellationToken ct = default) =>
        sender.Send(new UploadProductMediaRequest(file), ct);

    public Task<OpenedMediaDto> OpenMediaAsync(Guid id, bool publishedOnly, CancellationToken ct = default) =>
        publishedOnly
            ? sender.Send(new OpenPublicMediaRequest(id), ct)
            : sender.Send(new OpenProductMediaRequest(id, false), ct);
}

public sealed class OrderingTestClient(ISender sender)
{
    public ISender Sender => sender;

    public static OrderingTestClient ForScope(
        IServiceScope scope,
        TimeProvider? clock = null,
        OrderingSettings? settings = null,
        string? deniedPermission = null,
        ApplicationDbContext? dbOverride = null) =>
        new(CommerceTestSender.Create(
            scope,
            deniedPermission: deniedPermission,
            dbOverride: dbOverride,
            clock: clock,
            orderingSettings: settings));

    public Task<IReadOnlyList<OrderHistoryEntryDto>> GetOrderHistoryAsync(Guid orderId, CancellationToken ct = default) =>
        sender.Send(new GetAdminOrderHistoryRequest(orderId), ct);

    public Task<CartDto> GetCartAsync(CancellationToken ct = default) =>
        sender.Send(new GetMyCartRequest(), ct);

    public Task<CartDto> SaveCartAsync(SaveCart request, CancellationToken ct = default) =>
        sender.Send(new SaveMyCartRequest(request), ct);

    public Task<CartDto> SetDiscountAsync(SetCartDiscount request, CancellationToken ct = default) =>
        sender.Send(new SetMyCartDiscountRequest(request), ct);

    public Task<OrderDto> CheckoutAsync(Checkout request, CancellationToken ct = default) =>
        sender.Send(new CheckoutMyCartRequest(request), ct);

    public Task<OrderDto> GetOrderAsync(Guid id, bool admin, CancellationToken ct = default) =>
        admin
            ? sender.Send(new GetAdminOrderDetailRequest(id), ct)
            : sender.Send(new GetMyOrderDetailRequest(id), ct);

    public Task<Page<OrderSummary>> OrdersAsync(int page, int size, bool admin, CancellationToken ct = default) =>
        admin
            ? sender.Send(new GetAdminOrdersRequest(new AdminOrderQuery { Page = page, Size = size }), ct)
            : sender.Send(new GetMyOrdersRequest(page, size), ct);

    public Task<Page<OrderSummary>> OrdersAsync(AdminOrderQuery query, bool admin, CancellationToken ct = default) =>
        admin
            ? sender.Send(new GetAdminOrdersRequest(query), ct)
            : sender.Send(new GetMyOrdersRequest(query.Page, query.Size), ct);

    public Task<OrderDto> CancelAsync(Guid id, CancelOrder request, bool admin, CancellationToken ct = default) =>
        admin
            ? sender.Send(new CancelAdminOrderRequest(id, request), ct)
            : sender.Send(new CancelMyOrderRequest(id, request), ct);

    public Task<int> ExpireAsync(CancellationToken ct = default) =>
        sender.Send(new ExpireOrdersRequest(), ct);

    public Task<CartDto> GetGuestCartAsync(Guid guestId, CancellationToken ct = default) =>
        sender.Send(new GetGuestCartRequest(guestId), ct);

    public Task<CartDto> SaveGuestCartAsync(Guid guestId, SaveCart request, CancellationToken ct = default) =>
        sender.Send(new SaveGuestCartRequest(guestId, request), ct);

    public Task<CartDto> SetGuestDiscountAsync(Guid guestId, SetCartDiscount request, CancellationToken ct = default) =>
        sender.Send(new SetGuestCartDiscountRequest(guestId, request), ct);

    public Task<OrderDto> CheckoutGuestAsync(Guid guestId, string verifiedEmail, Checkout request, CancellationToken ct = default) =>
        sender.Send(new CheckoutGuestCartRequest(guestId, verifiedEmail, request), ct);

    public Task<Page<OrderSummary>> GuestOrdersAsync(string verifiedEmail, int page, int size, CancellationToken ct = default) =>
        sender.Send(new GetGuestOrdersRequest(verifiedEmail, page, size), ct);

    public Task<OrderDto> GetGuestOrderAsync(string verifiedEmail, Guid id, CancellationToken ct = default) =>
        sender.Send(new GetGuestOrderDetailRequest(verifiedEmail, id), ct);

    public Task<OrderDto> CancelGuestOrderAsync(string verifiedEmail, Guid id, CancelOrder request, CancellationToken ct = default) =>
        sender.Send(new CancelGuestOrderRequest(verifiedEmail, id, request), ct);

    public Task<OrderDto> ConfirmPaymentAsync(Guid orderId, ConfirmPayment request, CancellationToken ct = default) =>
        sender.Send(new ConfirmPaymentRequest(orderId, request), ct);

    public Task<OrderDto> RetryFulfillmentAsync(Guid orderId, RetryFulfillment request, CancellationToken ct = default) =>
        sender.Send(new RetryOrderFulfillmentRequest(orderId, request), ct);

    public Task<List<DigitalDeliveryDto>> DigitalDeliveriesAsync(Guid orderId, CancellationToken ct = default) =>
        sender.Send(new GetMyDigitalDeliveriesRequest(orderId), ct);

    public Task<List<DigitalDeliveryDto>> GuestDigitalDeliveriesAsync(Guid orderId, string verifiedEmail, CancellationToken ct = default) =>
        sender.Send(new GetGuestDigitalDeliveriesRequest(orderId, verifiedEmail), ct);

    public Task<ShipmentDto> DispatchShipmentAsync(Guid orderId, DispatchShipment request, CancellationToken ct = default) =>
        sender.Send(new DispatchShipmentRequest(orderId, request), ct);

    public Task<ShipmentDto> DeliverShipmentAsync(Guid orderId, Guid shipmentId, DeliverShipment request, CancellationToken ct = default) =>
        sender.Send(new DeliverShipmentRequest(orderId, shipmentId, request), ct);

    public Task<List<ShipmentDto>> ShipmentsAsync(Guid orderId, bool admin, CancellationToken ct = default) =>
        admin
            ? sender.Send(new GetAdminOrderShipmentsRequest(orderId), ct)
            : sender.Send(new GetMyOrderShipmentsRequest(orderId), ct);

    public Task<List<ShipmentDto>> GuestShipmentsAsync(Guid orderId, string verifiedEmail, CancellationToken ct = default) =>
        sender.Send(new GetGuestOrderShipmentsRequest(orderId, verifiedEmail), ct);

    public Task<List<RenewalDto>> RenewalsAsync(Guid orderId, CancellationToken ct = default) =>
        sender.Send(new GetMyOrderRenewalsRequest(orderId), ct);

    public Task<RenewalDto> SetRenewalTargetAsync(Guid orderId, Guid id, SetRenewalTarget request, CancellationToken ct = default) =>
        sender.Send(new SetRenewalTargetRequest(orderId, id, request), ct);

    public Task<RenewalContentDto> RevealRenewalTargetAsync(Guid orderId, Guid id, CancellationToken ct = default) =>
        sender.Send(new RevealMyRenewalTargetRequest(orderId, id), ct);

    public Task<Page<RenewalAdminDto>> RenewalsAdminAsync(int page, int size, RenewalStatus? status, CancellationToken ct = default, string? queue = null) =>
        sender.Send(new GetAdminRenewalsRequest(page, size, status, queue), ct);

    public Task<RenewalAdminDto> GetRenewalAdminAsync(Guid id, CancellationToken ct = default) =>
        sender.Send(new GetAdminRenewalDetailRequest(id), ct);

    public Task<List<RenewalDto>> ProvisionRenewalsAsync(Guid orderId, ProvisionRenewals request, CancellationToken ct = default) =>
        sender.Send(new ProvisionOrderRenewalsRequest(orderId, request), ct);

    public Task<RenewalAdminDto> ClaimRenewalAsync(Guid id, ClaimRenewal request, CancellationToken ct = default) =>
        sender.Send(new ClaimRenewalRequest(id, request), ct);

    public Task<RenewalAdminDto> ReleaseRenewalAsync(Guid id, ReleaseRenewal request, CancellationToken ct = default) =>
        sender.Send(new ReleaseRenewalRequest(id, request), ct);

    public Task<RenewalAdminDto> StartRenewalAsync(Guid id, StartRenewal request, CancellationToken ct = default) =>
        sender.Send(new StartRenewalRequest(id, request), ct);

    public Task<RenewalAdminDto> VerifyRenewalAsync(Guid id, VerifyRenewal request, CancellationToken ct = default) =>
        sender.Send(new VerifyRenewalRequest(id, request), ct);

    public Task<RenewalContentDto> RevealRenewalTargetAdminAsync(Guid id, CancellationToken ct = default) =>
        sender.Send(new RevealAdminRenewalTargetRequest(id), ct);

    public Task<RenewalContentDto> RevealRenewalEvidenceAsync(Guid id, Guid attemptId, CancellationToken ct = default) =>
        sender.Send(new RevealRenewalEvidenceRequest(id, attemptId), ct);

    public Task<List<RenewalHistoryDto>> RenewalHistoryAsync(Guid id, CancellationToken ct = default) =>
        sender.Send(new GetRenewalHistoryRequest(id), ct);

    public Task<AfterSalesDto> AfterSalesAsync(Guid orderId, bool admin, CancellationToken ct = default) =>
        admin
            ? sender.Send(new GetAdminOrderAfterSalesRequest(orderId), ct)
            : sender.Send(new GetMyOrderAfterSalesRequest(orderId), ct);

    public Task<RefundResult> ConfirmRefundAsync(Guid orderId, ConfirmRefund request, CancellationToken ct = default) =>
        sender.Send(new ConfirmRefundRequest(orderId, request), ct);

    public Task<ReturnResult> ReceiveReturnAsync(Guid orderId, ReceiveReturn request, CancellationToken ct = default) =>
        sender.Send(new ReceivePhysicalReturnRequest(orderId, request), ct);
}

public sealed class DiscountTestClient(ISender sender)
{
    public static DiscountTestClient ForScope(IServiceScope scope) =>
        new(CommerceTestSender.Create(scope));

    public Task<Page<DiscountDto>> ListAsync(int page, int size, string? search, bool? isActive, CancellationToken ct = default) =>
        sender.Send(new GetDiscountsRequest(page, size, search, isActive), ct);

    public Task<DiscountDto> GetAsync(Guid id, CancellationToken ct = default) =>
        sender.Send(new GetDiscountDetailRequest(id), ct);

    public Task<DiscountDto> CreateAsync(SaveDiscount request, CancellationToken ct = default) =>
        sender.Send(new CreateDiscountRequest(request), ct);

    public Task<DiscountDto> UpdateAsync(Guid id, SaveDiscount request, CancellationToken ct = default) =>
        sender.Send(new UpdateDiscountRequest(id, request), ct);
}

public sealed class GuestAccessTestClient(ISender sender)
{
    public static GuestAccessTestClient ForScope(IServiceScope scope, TimeProvider? clock = null) =>
        new(CommerceTestSender.Create(scope, clock: clock));

    public Task<bool> RequestCodeAsync(string email, CancellationToken ct = default) =>
        sender.Send(new RequestGuestEmailCodeRequest(email), ct);

    public Task<string> VerifyCodeAsync(string email, string code, CancellationToken ct = default) =>
        sender.Send(new VerifyGuestEmailCodeRequest(email, code), ct);
}
