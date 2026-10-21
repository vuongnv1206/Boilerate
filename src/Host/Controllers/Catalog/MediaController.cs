using Boilerate.Application.Catalog;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Identity.Users;
using Boilerate.Infrastructure.Auth.Permissions;
using Boilerate.Shared.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
namespace Boilerate.Host.Controllers.Catalog;

[Route("api/media")]
public sealed class MediaController(ICommerceMediaService media, ICurrentUser actor, IUserService users) : BaseApiController
{
    [HttpPost, Authorize, Consumes("multipart/form-data"), RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<MediaDto> Upload(IFormFile file, CancellationToken ct)
    {
        var userId = actor.GetUserId().ToString();
        if (!await users.HasPermissionAsync(userId, "Permissions.Product.Create", ct) &&
            !await users.HasPermissionAsync(userId, "Permissions.Product.Update", ct))
            throw new ForbiddenException("Uploading product images requires Product.Create or Product.Update.");
        return await media.UploadAsync(file, ct);
    }
    [HttpGet("{id:guid}"), MustHavePermission(AppAction.View, AppFunction.Product)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await media.OpenAsync(id, false, ct);
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.CacheControl = "no-store";
        return File(result.Stream, result.ContentType);
    }
}
