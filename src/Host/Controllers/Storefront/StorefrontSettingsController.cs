using Boilerate.Application.Storefront.Settings;
using Boilerate.Infrastructure.Auth.Permissions;
using Boilerate.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Boilerate.Host.Controllers.Storefront;

[Route("api/admin/storefront-settings"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminStorefrontSettingsController : BaseApiController
{
    [HttpGet, MustHavePermission(AppAction.View, AppFunction.StorefrontSettings)]
    public Task<AdminStorefrontSettingsDto> Get(CancellationToken ct) =>
        Mediator.Send(new GetAdminStorefrontSettingsRequest(), ct);

    [HttpPut, MustHavePermission(AppAction.Update, AppFunction.StorefrontSettings)]
    public Task<AdminStorefrontSettingsDto> Update(UpdateStorefrontSettingsRequest request, CancellationToken ct) =>
        Mediator.Send(request, ct);
}

[AllowAnonymous, Route("api/storefront/settings"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PublicStorefrontSettingsController : BaseApiController
{
    [HttpGet]
    public Task<GuestPaymentSettingsDto> Get(CancellationToken ct) =>
        Mediator.Send(new GetPublicPaymentSettingsRequest(), ct);
}
