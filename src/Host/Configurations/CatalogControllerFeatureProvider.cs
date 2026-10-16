using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Boilerate.Host.Configurations;

/// <summary>The model stays stable; only HTTP discovery depends on the startup flag.</summary>
internal sealed class CatalogControllerFeatureProvider(bool enabled, bool notificationsEnabled = true) : IApplicationFeatureProvider<ControllerFeature>
{
    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        if (enabled && notificationsEnabled) return;
        foreach (var controller in feature.Controllers.Where(c =>
                     (!enabled && c.Namespace == "Boilerate.Host.Controllers.Catalog") ||
                     (!notificationsEnabled && c.Name == "NotificationsController")).ToArray())
            feature.Controllers.Remove(controller);
    }
}
