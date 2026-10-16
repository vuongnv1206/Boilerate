using Boilerate.Application.Catalog.Products;
using Boilerate.Application.Common.FileStorage;
using Boilerate.Application.Common.Models;
using Boilerate.Infrastructure.Auth.Permissions;
using Boilerate.Shared.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSwag.Annotations;
using System.IO;

namespace Boilerate.Host.Controllers.Catalog;

[Route("api/products")]
public class ProductsController : BaseApiController
{
    [HttpPost("search")]
    [MustHavePermission(AppAction.View, AppFunction.Product)]
    [OpenApiOperation("Search products using available filters.", "")]
    public Task<PaginationResponse<ProductDto>> SearchAsync(SearchProductsRequest request)
    {
        return Mediator.Send(request);
    }

    [HttpGet("{id:guid}")]
    [MustHavePermission(AppAction.View, AppFunction.Product)]
    [OpenApiOperation("Get product details.", "")]
    public Task<ProductDto> GetAsync(Guid id)
    {
        return Mediator.Send(new GetProductRequest(id));
    }

    [HttpPost]
    [MustHavePermission(AppAction.Create, AppFunction.Product)]
    [OpenApiOperation("Create a new product.", "")]
    [Consumes("multipart/form-data")]
    public Task<Guid> CreateAsync([FromForm] CreateProductRequest request)
    {
        return Mediator.Send(request);
    }

    [HttpPut("{id:guid}")]
    [MustHavePermission(AppAction.Update, AppFunction.Product)]
    [OpenApiOperation("Update a product.", "")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<Guid>> UpdateAsync(Guid id, [FromForm] UpdateProductRequest request)
    {
        if (id != request.Id)
        {
            return BadRequest();
        }

        return Ok(await Mediator.Send(request));
    }

    [HttpDelete("{id:guid}")]
    [MustHavePermission(AppAction.Delete, AppFunction.Product)]
    [OpenApiOperation("Delete a product.", "")]
    public Task<Guid> DeleteAsync(Guid id)
    {
        return Mediator.Send(new DeleteProductRequest(id));
    }

    [HttpPost("restore/{id:guid}")]
    [MustHavePermission(AppAction.Update, AppFunction.Product)]
    [OpenApiOperation("Restore a soft-deleted product.", "")]
    public Task<Guid> RestoreAsync(Guid id)
    {
        return Mediator.Send(new RestoreProductRequest(id));
    }

    [HttpDelete("permanent/{id:guid}")]
    [MustHavePermission(AppAction.Clean, AppFunction.Product)]
    [OpenApiOperation("Permanently delete a soft-deleted product.", "")]
    public Task<Guid> PermanentDeleteAsync(Guid id)
    {
        return Mediator.Send(new PermanentDeleteProductRequest(id));
    }

    [HttpPost("deleted/search")]
    [MustHavePermission(AppAction.View, AppFunction.Product)] // Tuỳ vào chính sách có thể là Admin only (System)
    [OpenApiOperation("Search soft-deleted products using available filters.", "")]
    public Task<PaginationResponse<ProductDto>> SearchDeletedAsync(GetDeletedProductsRequest request)
    {
        return Mediator.Send(request);
    }

    [HttpPost("export")]
    [MustHavePermission(AppAction.Export, AppFunction.Product)]
    [OpenApiOperation("Export products to Excel or PDF.", "")]
    public async Task<FileResult> ExportAsync(ExportProductsRequest request)
    {
        var result = await Mediator.Send(request);
        
        if (request.ExportType?.ToLower() == "pdf")
        {
            return File(result, "application/pdf", "Products.pdf");
        }
        
        return File(result, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Products.xlsx");
    }
}
