using Boilerate.Application.Catalog.Categories;
using Boilerate.Application.Catalog.Products;
using Boilerate.Application.Storefront.PublicCatalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Boilerate.Host.Controllers.Storefront;

[AllowAnonymous, Route("api/catalog"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PublicCatalogController : BaseApiController
{
    [HttpPost("products/search")]
    public Task<Page<ProductSummary>> Search(ProductSearch request, CancellationToken ct) =>
        Mediator.Send(new SearchPublicProductsRequest(request), ct);

    [HttpGet("products/{slug}")]
    public Task<ProductDto> Get(string slug, CancellationToken ct) =>
        Mediator.Send(new GetPublicProductBySlugRequest(slug), ct);

    [HttpGet("categories")]
    public Task<List<PublicCategoryDto>> Categories(CancellationToken ct) =>
        Mediator.Send(new GetPublicCategoriesRequest(), ct);

    [HttpGet("media/{id:guid}")]
    public async Task<IActionResult> Media(Guid id, CancellationToken ct)
    {
        var result = await Mediator.Send(new OpenPublicMediaRequest(id), ct);
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.CacheControl = "no-store";
        return File(result.Stream, result.ContentType);
    }
}

