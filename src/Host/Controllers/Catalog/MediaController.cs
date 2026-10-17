using Boilerate.Application.Catalog;
using Boilerate.Infrastructure.Auth.Permissions;
using Boilerate.Shared.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Boilerate.Host.Controllers.Catalog;

[Route("api/media")]
public sealed class MediaController(ICommerceMediaService media) : BaseApiController
{
    [HttpPost, Consumes("multipart/form-data"), RequestSizeLimit(6 * 1024 * 1024), MustHavePermission(AppAction.Update, AppFunction.Product)]
    public Task<MediaDto> Upload(IFormFile file, CancellationToken ct) => media.UploadAsync(file, ct);
    [HttpGet("{id:guid}"), MustHavePermission(AppAction.View, AppFunction.Product)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await media.OpenAsync(id, false, ct);
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.CacheControl = "no-store";
        return File(result.Stream, result.ContentType);
    }
}