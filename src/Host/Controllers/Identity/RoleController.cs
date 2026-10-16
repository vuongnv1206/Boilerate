using Boilerate.Infrastructure.Auth.Permissions;
using Boilerate.Shared.Authorization;
using Boilerate.Application.Identity.Roles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Boilerate.Host.Controllers.Identity;
[Route("api/roles")]
[Authorize(Policy = "CentralAdmin")]
public sealed class RoleController(IRoleService service) : BaseApiController
{
    [HttpGet] [MustHavePermission(AppAction.View, AppFunction.Role)]  public Task<List<RoleDto>> List(CancellationToken ct) => service.GetListAsync(ct);
    [HttpGet("{id}")] [MustHavePermission(AppAction.View, AppFunction.Role)]  public Task<RoleDto> Get(string id) => service.GetByIdAsync(id);
    [HttpGet("{id}/permissions")] [MustHavePermission(AppAction.View, AppFunction.Role)]  public Task<List<FunctionDto>> Permissions(string id, CancellationToken ct) => service.GetByIdWithPermissionsAsync(id, ct);
    [HttpPost] [MustHavePermission(AppAction.Create, AppFunction.Role)]  public Task<string> Create(CreateOrUpdateRoleRequest request)
        { request.Id = null; return service.CreateOrUpdateAsync(request); }
    [HttpPut("{id}")] [MustHavePermission(AppAction.Update, AppFunction.Role)]  public Task<string> Update(string id, CreateOrUpdateRoleRequest request)
        { request.Id = id; return service.CreateOrUpdateAsync(request); }
    [HttpPut("{id}/permissions")] [MustHavePermission(AppAction.Update, AppFunction.Role)]  public Task<string> SetPermissions(string id, UpdateRolePermissionsRequest request, CancellationToken ct)
        { request.RoleId = id; return service.UpdatePermissionsAsync(request, ct); }
    [HttpDelete("{id}")] [MustHavePermission(AppAction.Delete, AppFunction.Role)]  public Task<string> Delete(string id, [FromQuery] string version) => service.DeleteAsync(id, version);
}
