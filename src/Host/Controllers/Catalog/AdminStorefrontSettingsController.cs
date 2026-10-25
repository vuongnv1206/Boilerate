using Boilerate.Application.Ordering;
using Boilerate.Infrastructure.Auth.Permissions;
using Boilerate.Shared.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Boilerate.Host.Controllers.Catalog;

[Route("api/admin/storefront-settings"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminStorefrontSettingsController(IStorefrontSettingsService settings) : BaseApiController
{
    [HttpGet, MustHavePermission(AppAction.View, AppFunction.StorefrontSettings)]
    public Task<AdminStorefrontSettingsDto> Get(CancellationToken ct) => settings.GetAdminAsync(ct);

    [HttpPut, MustHavePermission(AppAction.Update, AppFunction.StorefrontSettings)]
    public Task<AdminStorefrontSettingsDto> Update(UpdateStorefrontSettings request, CancellationToken ct) => settings.UpdateAsync(request, ct);
}
