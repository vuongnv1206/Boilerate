using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Boilerate.Host.Configurations;

/// <summary>The model stays stable; only HTTP discovery depends on the startup flag.</summary>
internal sealed class CatalogControllerFeatureProvider(bool enabled, bool notificationsEnabled = true) : IApplicationFeatureProvider<ControllerFeature>
{
    private static readonly HashSet<string> CommerceNamespaces = new(StringComparer.Ordinal)
    {
        "Boilerate.Host.Controllers.Catalog",
        "Boilerate.Host.Controllers.Inventory",
        "Boilerate.Host.Controllers.Storefront",
        "Boilerate.Host.Controllers.Ordering",
        "Boilerate.Host.Controllers.Payments",
        "Boilerate.Host.Controllers.Shipping",
        "Boilerate.Host.Controllers.Renewals",
        "Boilerate.Host.Controllers.AfterSales",
        "Boilerate.Host.Controllers.Overview",
        "Boilerate.Host.Controllers.Outbox",
    };

    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        if (enabled && notificationsEnabled)
        {
            return;
        }

        foreach (var controller in feature.Controllers.Where(c =>
                     (!enabled && c.Namespace is not null && CommerceNamespaces.Contains(c.Namespace)) ||
                     (!notificationsEnabled && c.Name == "NotificationsController")).ToArray())
        {
            feature.Controllers.Remove(controller);
        }
    }
}