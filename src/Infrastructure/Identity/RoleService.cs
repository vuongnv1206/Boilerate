using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Identity.Roles;
using Boilerate.Domain.Identity;
using Boilerate.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Infrastructure.Identity;

internal sealed class RoleService : IRoleService
{
    private readonly ApplicationDbContext _db;
    private readonly RoleManager<ApplicationRole> _roles;
    private readonly AclGuard _guard;
    private readonly IServiceProvider _services;
    public RoleService(ApplicationDbContext db, RoleManager<ApplicationRole> roles, AclGuard guard, IServiceProvider services)
        => (_db, _roles, _guard, _services) = (db, roles, guard, services);
    private static RoleDto Map(ApplicationRole r) => new()
        { Id = r.Id, Name = r.Name!, Description = r.Description, Version = r.ConcurrencyStamp! };
    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded) throw new ConflictException(string.Join("; ", result.Errors.Select(x => x.Description)));
    }
    public async Task<List<RoleDto>> GetListAsync(CancellationToken cancellationToken)
    {
        await _guard.RequireAdminAsync(cancellationToken);
        return (await _db.Roles.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken)).Select(Map).ToList();
    }
    public Task<int> GetCountAsync(CancellationToken cancellationToken) => _db.Roles.CountAsync(cancellationToken);
    public async Task<bool> ExistsAsync(string roleName, string? excludeId) =>
        await _db.Roles.AnyAsync(x => x.NormalizedName == roleName.ToUpperInvariant() && x.Id != excludeId);
    public async Task<RoleDto> GetByIdAsync(string id)
    {
        await _guard.RequireAdminAsync(default);
        return Map(await _db.Roles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new NotFoundException("Role not found."));
    }
    public async Task<List<FunctionDto>> GetByIdWithPermissionsAsync(string roleId, CancellationToken cancellationToken)
    {
        await _guard.RequireAdminAsync(cancellationToken);
        if (!await _db.Roles.AnyAsync(x => x.Id == roleId, cancellationToken)) throw new NotFoundException("Role not found.");
        var functions = await _db.Functions.AsNoTracking().Include(x => x.ActionInFunctions).ThenInclude(x => x.Action).ToListAsync(cancellationToken);
        var grants = await _db.Permissions.Where(x => x.RoleId == roleId).ToListAsync(cancellationToken);
        return functions.Select(f => new FunctionDto
        {
            Id = f.Id, GroupId = f.GroupId, Code = f.Code, Name = f.Name, IsActive = f.IsActive, Version = f.Version,
            ActionDtos = f.ActionInFunctions.Select(p => new ActionDto
            {
                Id = p.ActionId, Code = p.Action.Code, Name = p.Action.Name, Version = p.Action.Version,
                IsActive = p.IsActive && p.Action.IsActive,
                Selected = grants.Any(g => g.FunctionId == f.Id && g.ActionId == p.ActionId)
            }).ToList()
        }).ToList();
    }
    public async Task<string> CreateOrUpdateAsync(CreateOrUpdateRoleRequest request)
    {
        await _guard.RequireAdminAsync(default);
        await RequestValidation.ValidateAsync(_services, request);
        await using var tx = await _db.Database.BeginTransactionAsync();
        await _guard.LockAsync(default);
        if (string.IsNullOrEmpty(request.Id))
        {
            var role = new ApplicationRole(request.Name, request.Description);
            Check(await _roles.CreateAsync(role));
            _guard.Audit("Roles", role.Id, null, new { role.Name, role.Description });
            await _db.SaveChangesAsync(); await tx.CommitAsync(); return role.Id;
        }
        var existing = await _roles.FindByIdAsync(request.Id) ?? throw new NotFoundException("Role not found.");
        AclGuard.CheckVersion(request.Version, existing.ConcurrencyStamp);
        if (existing.NormalizedName == "ADMIN" || existing.NormalizedName == "BASIC")
            throw new ConflictException("Bootstrap roles cannot be renamed.");
        var before = new { existing.Name, existing.Description };
        existing.Name = request.Name; existing.Description = request.Description;
        Check(await _roles.UpdateAsync(existing));
        _guard.Audit("Roles", existing.Id, before, new { existing.Name, existing.Description });
        await _db.SaveChangesAsync(); await tx.CommitAsync(); return existing.Id;
    }
    public async Task<string> UpdatePermissionsAsync(UpdateRolePermissionsRequest request, CancellationToken cancellationToken)
    {
        await _guard.RequireAdminAsync(cancellationToken);
        await RequestValidation.ValidateAsync(_services, request, cancellationToken);
        var wanted = request.Permissions.Select(p => (p.FunctionId, p.ActionId)).ToHashSet();
        if (wanted.Count != request.Permissions.Count) throw new FluentValidation.ValidationException("Duplicate grants.");
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        await _guard.LockAsync(cancellationToken);
        var role = await _roles.FindByIdAsync(request.RoleId) ?? throw new NotFoundException("Role not found.");
        AclGuard.CheckVersion(request.Version, role.ConcurrencyStamp);
        var valid = (await _db.ActionInFunctions.Where(p => p.IsActive && p.Action.IsActive && p.Function.IsActive)
            .Select(p => new { p.FunctionId, p.ActionId }).ToListAsync(cancellationToken))
            .Select(p => (p.FunctionId, p.ActionId)).ToHashSet();
        var current = await _db.Permissions.IgnoreQueryFilters().Where(p => p.RoleId == role.Id).ToListAsync(cancellationToken);
        // Existing inactive grants may be retained or revoked, never newly assigned/reactivated.
        valid.UnionWith(current.Where(p => p.DeletedOn == null).Select(p => (p.FunctionId, p.ActionId)));
        if (!wanted.IsSubsetOf(valid)) throw new FluentValidation.ValidationException("Unknown or inactive action/function pair.");
        if (role.NormalizedName == "ADMIN")
        {
            var administrationPairs = (await _db.ActionInFunctions.Where(p => p.IsActive)
                .Select(p => new { p.FunctionId, p.ActionId, Function = p.Function.NormalizedCode, Action = p.Action.NormalizedCode })
                .ToListAsync(cancellationToken)).Where(p => AclGuard.IsManagementPair(p.Function, p.Action))
                .Select(p => (p.FunctionId, p.ActionId)).ToHashSet();
            if (current.Any(p => p.DeletedOn == null && administrationPairs.Contains((p.FunctionId, p.ActionId)) && !wanted.Contains((p.FunctionId, p.ActionId))))
                throw new ConflictException("Administration grants cannot be revoked from the protected role.");
        }
        var before = current.Where(p => p.DeletedOn == null).Select(p => new { p.FunctionId, p.ActionId }).ToArray();
        foreach (var grant in current)
        {
            if (wanted.Contains((grant.FunctionId, grant.ActionId))) { grant.DeletedOn = null; grant.DeletedBy = null; }
            else if (grant.DeletedOn == null) _db.Permissions.Remove(grant);
        }
        foreach (var pair in wanted.Where(p => !current.Any(g => g.FunctionId == p.FunctionId && g.ActionId == p.ActionId)))
            _db.Permissions.Add(new Permission(role.Id, pair.FunctionId, pair.ActionId));
        Check(await _roles.UpdateAsync(role));
        _guard.Audit("RolePermissions", role.Id, before, request.Permissions);
        await _db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken);
        return "Permissions updated.";
    }
    public async Task<string> DeleteAsync(string id, string version)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();
        await _guard.LockAsync(default);
        var role = await _roles.FindByIdAsync(id) ?? throw new NotFoundException("Role not found.");
        AclGuard.CheckVersion(version, role.ConcurrencyStamp);
        if (role.NormalizedName is "ADMIN" or "BASIC" || await _db.UserRoles.AnyAsync(x => x.RoleId == id) ||
            await _db.Permissions.IgnoreQueryFilters().AnyAsync(x => x.RoleId == id))
            throw new ConflictException("A protected or referenced role cannot be deleted.");
        _guard.Audit("Roles", id, new { role.Name, role.Description }, null);
        Check(await _roles.DeleteAsync(role));
        await _db.SaveChangesAsync(); await tx.CommitAsync(); return "Role deleted.";
    }
}
