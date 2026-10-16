using Boilerate.Application.Catalog.Categories;
using Boilerate.Application.Common.Models;
using Boilerate.Infrastructure.Auth.Permissions;
using Boilerate.Shared.Authorization;
using Microsoft.AspNetCore.Mvc;
using NSwag.Annotations;

namespace Boilerate.Host.Controllers.Catalog;

[Route("api/categories")]
public class CategoriesController : BaseApiController
{
    [HttpPost("search")]
    [MustHavePermission(AppAction.View, AppFunction.Category)]
    [OpenApiOperation("Search categories using available filters.", "")]
    public Task<PaginationResponse<CategoryDto>> SearchAsync(SearchCategoriesRequest request)
    {
        return Mediator.Send(request);
    }

    [HttpGet("{id:guid}")]
    [MustHavePermission(AppAction.View, AppFunction.Category)]
    [OpenApiOperation("Get category details.", "")]
    public Task<CategoryDto> GetAsync(Guid id)
    {
        return Mediator.Send(new GetCategoryRequest(id));
    }

    [HttpPost]
    [MustHavePermission(AppAction.Create, AppFunction.Category)]
    [OpenApiOperation("Create a new category.", "")]
    public Task<Guid> CreateAsync(CreateCategoryRequest request)
    {
        return Mediator.Send(request);
    }

    [HttpPut("{id:guid}")]
    [MustHavePermission(AppAction.Update, AppFunction.Category)]
    [OpenApiOperation("Update a category.", "")]
    public async Task<ActionResult<Guid>> UpdateAsync(Guid id, UpdateCategoryRequest request)
    {
        if (id != request.Id)
        {
            return BadRequest();
        }
        return Ok(await Mediator.Send(request));
    }

    [HttpDelete("{id:guid}")]
    [MustHavePermission(AppAction.Delete, AppFunction.Category)]
    [OpenApiOperation("Delete a category.", "")]
    public Task<Guid> DeleteAsync(Guid id)
    {
        return Mediator.Send(new DeleteCategoryRequest(id));
    }
}
