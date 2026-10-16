using Boilerate.Infrastructure.Auth.Permissions;
using Boilerate.Shared.Authorization;
using Boilerate.Application.Identity.Roles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Boilerate.Host.Controllers.Identity;

[Authorize(Policy = "CentralAdmin")]
public sealed class ActionsController(ICapabilityService service) : BaseApiController
{
    [HttpGet] [MustHavePermission(AppAction.View, AppFunction.Action)]  public Task<List<ActionDto>> List(CancellationToken ct) => service.GetActionsAsync(ct);
    [HttpGet("{id:guid}")] [MustHavePermission(AppAction.View, AppFunction.Action)]  public Task<ActionDto> Get(Guid id, CancellationToken ct) => service.GetActionAsync(id, ct);
    [HttpPost] [MustHavePermission(AppAction.Create, AppFunction.Action)]  public Task<ActionDto> Create(CapabilityRequest request, CancellationToken ct) => service.CreateActionAsync(request, ct);
    [HttpPut("{id:guid}")] [MustHavePermission(AppAction.Update, AppFunction.Action)]  public Task<ActionDto> Update(Guid id, CapabilityRequest request, CancellationToken ct) => service.UpdateActionAsync(id, request, ct);
    [HttpDelete("{id:guid}")] [MustHavePermission(AppAction.Delete, AppFunction.Action)]  public async Task<IActionResult> Delete(Guid id, [FromQuery] string version, CancellationToken ct)
        { await service.DeleteActionAsync(id, version, ct); return NoContent(); }
}
