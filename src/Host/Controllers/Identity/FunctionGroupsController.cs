using Boilerate.Application.Identity.Roles;
using Boilerate.Infrastructure.Auth.Permissions;
using Boilerate.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Boilerate.Host.Controllers.Identity;

[Route("api/function-groups")]
[Authorize(Policy = "CentralAdmin")]
public sealed class FunctionGroupsController(IFunctionGroupService service) : BaseApiController
{
    [HttpGet, MustHavePermission(AppAction.View, AppFunction.Function)]
    public Task<List<FunctionGroupDto>> List(CancellationToken ct) => service.ListAsync(ct);

    [HttpPost, MustHavePermission(AppAction.Create, AppFunction.Function)]
    public Task<FunctionGroupDto> Create(FunctionGroupRequest request, CancellationToken ct) => service.CreateAsync(request, ct);

    [HttpPut("{id:guid}"), MustHavePermission(AppAction.Update, AppFunction.Function)]
    public Task<FunctionGroupDto> Update(Guid id, FunctionGroupRequest request, CancellationToken ct) => service.UpdateAsync(id, request, ct);

    [HttpDelete("{id:guid}"), MustHavePermission(AppAction.Delete, AppFunction.Function)]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] string version, CancellationToken ct)
    {
        await service.DeleteAsync(id, version, ct);
        return NoContent();
    }
}
