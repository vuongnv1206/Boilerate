using Boilerate.Application.Catalog.Categories;
using Boilerate.Domain.Catalog;
using Boilerate.Infrastructure.Auth.Permissions;
using Boilerate.Shared.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Boilerate.Host.Controllers.Catalog;

[Route("api/categories")]
public sealed class CategoriesController : BaseApiController
{
    [HttpGet, MustHavePermission(AppAction.View, AppFunction.Category)]
    public Task<List<Category>> Get(CancellationToken ct) =>
        Mediator.Send(new GetCategoriesRequest(false), ct);

    [HttpGet("summaries"), MustHavePermission(AppAction.View, AppFunction.Category)]
    public Task<List<CategorySummary>> Summaries(CancellationToken ct) =>
        Mediator.Send(new GetCategorySummariesRequest(), ct);

    [HttpPost, MustHavePermission(AppAction.Create, AppFunction.Category)]
    public async Task<ActionResult<Category>> Create(CreateCategoryRequest request, CancellationToken ct) =>
        request.Id.HasValue ? BadRequest() : Ok(await Mediator.Send(request, ct));

    [HttpPut("{id:guid}"), MustHavePermission(AppAction.Update, AppFunction.Category)]
    public async Task<ActionResult<Category>> Edit(Guid id, UpdateCategoryRequest request, CancellationToken ct) =>
        id != request.Id ? BadRequest() : Ok(await Mediator.Send(request, ct));

    [HttpDelete("{id:guid}"), MustHavePermission(AppAction.Delete, AppFunction.Category)]
    public Task<bool> Delete(Guid id, [FromQuery] string version, CancellationToken ct) =>
        Mediator.Send(new DeleteCategoryRequest(id, version), ct);

    [HttpPut("{id:guid}/products"), MustHavePermission(AppAction.Update, AppFunction.Product)]
    public async Task<ActionResult<List<CategoryProductResult>>> SetProducts(
        Guid id,
        SetCategoryProductsRequest request,
        CancellationToken ct) =>
        id != request.CategoryId ? BadRequest() : Ok(await Mediator.Send(request, ct));
}
