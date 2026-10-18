namespace Boilerate.Shared.Authorization;

/// <summary>
/// Functions (modules/features) available in the system
/// Used to build permissions: Permissions.{Function}.{Action}
/// </summary>
public static class AppFunction
{
    public const string Action = nameof(Action);
    public const string Function = nameof(Function);
    public const string Dashboard = nameof(Dashboard);
    public const string Hangfire = nameof(Hangfire);
    public const string User = nameof(User);
    public const string Users = nameof(Users);
    public const string UserRole = nameof(UserRole);
    public const string Role = nameof(Role);
    public const string Roles = nameof(Roles);
    public const string RoleClaim = nameof(RoleClaim);
    public const string Product = nameof(Product);
    public const string Products = nameof(Products);
    public const string Category = nameof(Category);
    public const string Categories = nameof(Categories);
    public const string ProductType = nameof(ProductType);
    public const string Price = nameof(Price);
    public const string Inventory = nameof(Inventory);
    public const string DigitalInventory = nameof(DigitalInventory);
    public const string Order = nameof(Order);
    public const string Payment = nameof(Payment);
    public const string Outbox = nameof(Outbox);
    public const string Notifications = nameof(Notifications);
}
