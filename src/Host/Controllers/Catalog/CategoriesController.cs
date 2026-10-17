using Boilerate.Application.Catalog;
using Boilerate.Domain.Catalog;
using Boilerate.Infrastructure.Auth.Permissions;
using Boilerate.Shared.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Boilerate.Host.Controllers.Catalog;

[Route("api/categories")]
public sealed class CategoriesController(ICommerceService commerce) : BaseApiController
{
    [HttpGet, MustHavePermission(AppAction.View, AppFunction.Category)]
    public Task<List<Category>> Get(CancellationToken ct) => commerce.CategoriesAsync(false, ct);
    [HttpPost, MustHavePermission(AppAction.Create, AppFunction.Category)]
    public async Task<ActionResult<Category>> Create(SaveCategory request, CancellationToken ct)
        => request.Id.HasValue ? BadRequest() : Ok(await Mediator.Send(request, ct));
    [HttpPut("{id:guid}"), MustHavePermission(AppAction.Update, AppFunction.Category)]
    public async Task<ActionResult<Category>> Edit(Guid id, SaveCategory request, CancellationToken ct)
        => id != request.Id ? BadRequest() : Ok(await Mediator.Send(request, ct));
    [HttpDelete("{id:guid}"), MustHavePermission(AppAction.Delete, AppFunction.Category)]
    public Task<bool> Delete(Guid id, [FromQuery] string version, CancellationToken ct) => Mediator.Send(new DeleteCategory(id, version), ct);
}