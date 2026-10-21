using Boilerate.Domain.Common.Contracts;

namespace Boilerate.Domain.Identity;

/// <summary>Presentation grouping only; never participates in grant evaluation.</summary>
public sealed class FunctionGroup : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string NormalizedCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public string Version { get; set; } = Guid.NewGuid().ToString("N");

    public static readonly Guid SystemId = Guid.Parse("cd9a1000-0000-4000-8000-000000000001");
    public static readonly Guid CatalogId = Guid.Parse("cd9a1000-0000-4000-8000-000000000002");
    public static readonly Guid OrdersId = Guid.Parse("cd9a1000-0000-4000-8000-000000000003");
    public static readonly Guid OtherId = Guid.Parse("cd9a1000-0000-4000-8000-000000000004");

    public static Guid InitialGroupFor(string code) => code.ToUpperInvariant() switch
    {
        "ACTION" or "FUNCTION" or "ROLE" or "USER" or "USERS" or "HANGFIRE" or "OUTBOX" or "NOTIFICATIONS" => SystemId,
        "PRODUCT" or "PRODUCTS" or "CATEGORY" or "CATEGORIES" or "PRODUCTTYPE" or "PRICE" or "INVENTORY" or "DIGITALINVENTORY" => CatalogId,
        "ORDER" or "PAYMENT" or "SHIPPING" or "RENEWAL" or "AFTERSALES" or "REFUND" or "RETURN" => OrdersId,
        _ => OtherId
    };

    public static FunctionGroup[] InitialGroups() =>
    [
        new() { Id = SystemId, Code = "System", NormalizedCode = "SYSTEM", Name = "Hệ thống", SortOrder = 10, Version = "initial" },
        new() { Id = CatalogId, Code = "Catalog", NormalizedCode = "CATALOG", Name = "Danh mục và sản phẩm", SortOrder = 20, Version = "initial" },
        new() { Id = OrdersId, Code = "Orders", NormalizedCode = "ORDERS", Name = "Đơn hàng", SortOrder = 30, Version = "initial" },
        new() { Id = OtherId, Code = "Other", NormalizedCode = "OTHER", Name = "Chức năng khác", SortOrder = 99, Version = "initial" }
    ];
}
