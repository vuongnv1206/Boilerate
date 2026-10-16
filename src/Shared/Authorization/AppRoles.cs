using System.Collections.ObjectModel;

namespace Boilerate.Shared.Authorization;

/// <summary>
/// Default roles trong hệ thống
/// </summary>
public static class AppRoles
{
    public const string Admin = nameof(Admin);
    public const string Basic = nameof(Basic);

    public static IReadOnlyList<string> DefaultRoles { get; } = new ReadOnlyCollection<string>(new[]
    {
        Admin,
        Basic
    });

    public static bool IsDefault(string roleName) => 
        DefaultRoles.Any(r => r.Equals(roleName, StringComparison.OrdinalIgnoreCase));
}
