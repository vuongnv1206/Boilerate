using Boilerate.Infrastructure.Auth.Permissions;
using Boilerate.Shared.Authorization;
using Boilerate.Application.Identity.Roles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Boilerate.Host.Controllers.Identity;

[Authorize(Policy = "CentralAdmin")]
public sealed class FunctionsController(ICapabilityService service) : BaseApiController
{
    [HttpGet] [MustHavePermission(AppAction.View, AppFunction.Function)]  public Task<List<FunctionDto>> List(CancellationToken ct) => service.GetFunctionsAsync(ct);
    [HttpGet("{id:guid}")] [MustHavePermission(AppAction.View, AppFunction.Function)]  public Task<FunctionDto> Get(Guid id, CancellationToken ct) => service.GetFunctionAsync(id, ct);
    [HttpPost] [MustHavePermission(AppAction.Create, AppFunction.Function)]  public Task<FunctionDto> Create(FunctionRequest request, CancellationToken ct) => service.CreateFunctionAsync(request, ct);
    [HttpPut("{id:guid}")] [MustHavePermission(AppAction.Update, AppFunction.Function)]  public Task<FunctionDto> Update(Guid id, FunctionRequest request, CancellationToken ct) => service.UpdateFunctionAsync(id, request, ct);
    [HttpPut("{id:guid}/actions")] [MustHavePermission(AppAction.Update, AppFunction.Function)]  public Task<FunctionDto> SetActions(Guid id, FunctionActionsRequest request, CancellationToken ct) => service.UpdateActionsAsync(id, request, ct);
    [HttpDelete("{id:guid}")] [MustHavePermission(AppAction.Delete, AppFunction.Function)]  public async Task<IActionResult> Delete(Guid id, [FromQuery] string version, CancellationToken ct)
        { await service.DeleteFunctionAsync(id, version, ct); return NoContent(); }
}
