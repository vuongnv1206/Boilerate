using Boilerate.Application.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Boilerate.Host.Controllers.Catalog;

[AllowAnonymous, Route("api/catalog"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CatalogController(ICommerceService commerce, ICommerceMediaService media) : BaseApiController
{
    [HttpPost("products/search")]
    public Task<Page<ProductSummary>> Search(ProductSearch request, CancellationToken ct) => commerce.SearchAsync(request, true, ct);
    [HttpGet("products/{slug}")]
    public Task<ProductDto> Get(string slug, CancellationToken ct) => commerce.GetBySlugAsync(slug, ct);
    [HttpGet("categories")]
    public async Task<object> Categories(CancellationToken ct) => (await commerce.CategoriesAsync(true, ct)).Select(c => new { c.Id, c.ParentId, c.Name, c.Slug, c.Description, c.SortOrder });
    [HttpGet("media/{id:guid}")]
    public async Task<IActionResult> Media(Guid id, CancellationToken ct)
    {
        var result = await media.OpenAsync(id, true, ct);
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.CacheControl = "no-store";
        return File(result.Stream, result.ContentType);
    }
}