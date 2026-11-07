using Boilerate.Application.Catalog.Media;
using Boilerate.Infrastructure.Auth.Permissions;
using Boilerate.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Boilerate.Host.Controllers.Catalog;

[Route("api/media")]
public sealed class MediaController : BaseApiController
{
    [HttpPost, Authorize, Consumes("multipart/form-data"), RequestSizeLimit(6 * 1024 * 1024)]
    public Task<MediaDto> Upload(IFormFile file, CancellationToken ct) =>
        Mediator.Send(new UploadProductMediaRequest(file), ct);

    [HttpGet("{id:guid}"), MustHavePermission(AppAction.View, AppFunction.Product)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await Mediator.Send(new OpenProductMediaRequest(id, false), ct);
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.CacheControl = "no-store";
        return File(result.Stream, result.ContentType);
    }
}
