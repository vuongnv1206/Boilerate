using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Identity.Roles;
using Boilerate.Domain.Identity;
using Boilerate.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using ActionEntity = Boilerate.Domain.Identity.Action;

namespace Boilerate.Infrastructure.Identity;

internal sealed class CapabilityService : ICapabilityService
{
    private readonly ApplicationDbContext _db;
    private readonly AclGuard _guard;
    public CapabilityService(ApplicationDbContext db, AclGuard guard) => (_db, _guard) = (db, guard);

    private static ActionDto Map(ActionEntity a) => new()
        { Id = a.Id, Code = a.Code, Name = a.Name, IsActive = a.IsActive, Version = a.Version };
    private static FunctionDto Map(Function f) => new()
    {
        Id = f.Id, Code = f.Code, Name = f.Name, IsActive = f.IsActive, Version = f.Version,
        ActionDtos = f.ActionInFunctions.Select(p => new ActionDto
        {
            Id = p.ActionId, Code = p.Action.Code, Name = p.Action.Name, Version = p.Action.Version,
            IsActive = p.IsActive && p.Action.IsActive, Selected = p.IsActive
        }).ToList()
    };
    private static void Validate(CapabilityRequest request)
    {
        AclGuard.NormalizeCode(request.Code);
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 100)
            throw new FluentValidation.ValidationException("Name must contain 1-100 characters.");
    }
    public async Task<List<ActionDto>> GetActionsAsync(CancellationToken ct)
    {
        await _guard.RequireAdminAsync(ct);
        return (await _db.Actions.AsNoTracking().OrderBy(x => x.Code).ToListAsync(ct)).Select(Map).ToList();
    }
    public async Task<ActionDto> GetActionAsync(Guid id, CancellationToken ct)
    {
        await _guard.RequireAdminAsync(ct);
        return Map(await _db.Actions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Action not found."));
    }
    public async Task<List<FunctionDto>> GetFunctionsAsync(CancellationToken ct)
    {
        await _guard.RequireAdminAsync(ct);
        return (await _db.Functions.AsNoTracking().Include(x => x.ActionInFunctions).ThenInclude(x => x.Action)
            .OrderBy(x => x.Code).ToListAsync(ct)).Select(Map).ToList();
    }
    public async Task<FunctionDto> GetFunctionAsync(Guid id, CancellationToken ct)
    {
        await _guard.RequireAdminAsync(ct);
        return Map(await _db.Functions.AsNoTracking().Include(x => x.ActionInFunctions).ThenInclude(x => x.Action)
            .SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Function not found."));
    }
    public async Task<ActionDto> CreateActionAsync(CapabilityRequest request, CancellationToken ct)
    {
        Validate(request);
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await _guard.LockAsync(ct);
        var code = AclGuard.NormalizeCode(request.Code);
        if (await _db.Actions.AnyAsync(x => x.NormalizedCode == code, ct))
            throw new ConflictException("Action code already exists.");
        var entity = new ActionEntity(request.Code) { Name = request.Name, IsActive = request.IsActive };
        _db.Actions.Add(entity);
        _guard.Audit("Actions", entity.Id.ToString(), null, new { entity.Code, entity.Name, entity.IsActive });
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Map(entity);
    }
    public async Task<FunctionDto> CreateFunctionAsync(CapabilityRequest request, CancellationToken ct)
    {
        Validate(request);
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await _guard.LockAsync(ct);
        var code = AclGuard.NormalizeCode(request.Code);
        if (await _db.Functions.AnyAsync(x => x.NormalizedCode == code, ct))
            throw new ConflictException("Function code already exists.");
        var entity = new Function { Code = request.Code, NormalizedCode = code, Name = request.Name, IsActive = request.IsActive };
        _db.Functions.Add(entity);
        _guard.Audit("Functions", entity.Id.ToString(), null, new { entity.Code, entity.Name, entity.IsActive });
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Map(entity);
    }
    public async Task<ActionDto> UpdateActionAsync(Guid id, CapabilityRequest request, CancellationToken ct)
    {
        Validate(request);
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await _guard.LockAsync(ct);
        var entity = await _db.Actions.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Action not found.");
        AclGuard.CheckVersion(request.Version, entity.Version);
        if (request.Code != entity.Code) throw new ConflictException("Code is immutable.");
        var usedPairs = await _db.ActionInFunctions.Where(p => p.ActionId == id && p.IsActive)
            .Select(p => new { Function = p.Function.NormalizedCode, Action = p.Action.NormalizedCode }).ToListAsync(ct);
        if (!request.IsActive && usedPairs.Any(p => AclGuard.IsManagementPair(p.Function, p.Action)))
            throw new ConflictException("An administration action cannot be disabled.");
        var before = new { entity.Code, entity.Name, entity.IsActive };
        entity.Name = request.Name; entity.IsActive = request.IsActive; entity.Version = Guid.NewGuid().ToString("N");
        _guard.Audit("Actions", id.ToString(), before, new { entity.Code, entity.Name, entity.IsActive });
        await _db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Map(entity);
    }
    public async Task<FunctionDto> UpdateFunctionAsync(Guid id, CapabilityRequest request, CancellationToken ct)
    {
        Validate(request);
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await _guard.LockAsync(ct);
        var entity = await _db.Functions.Include(x => x.ActionInFunctions).ThenInclude(x => x.Action)
            .SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Function not found.");
        AclGuard.CheckVersion(request.Version, entity.Version);
        if (request.Code != entity.Code) throw new ConflictException("Code is immutable.");
        if (!request.IsActive && AclGuard.IsManagementFunction(entity.Code))
            throw new ConflictException("An administration function cannot be disabled.");
        var before = new { entity.Code, entity.Name, entity.IsActive };
        entity.Name = request.Name; entity.IsActive = request.IsActive; entity.Version = Guid.NewGuid().ToString("N");
        _guard.Audit("Functions", id.ToString(), before, new { entity.Code, entity.Name, entity.IsActive });
        await _db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Map(entity);
    }
    public async Task DeleteActionAsync(Guid id, string version, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await _guard.LockAsync(ct);
        var entity = await _db.Actions.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Action not found.");
        AclGuard.CheckVersion(version, entity.Version);
        if (await _db.ActionInFunctions.AnyAsync(x => x.ActionId == id, ct) ||
            await _db.Permissions.IgnoreQueryFilters().AnyAsync(x => x.ActionId == id, ct))
            throw new ConflictException("Referenced actions must be disabled instead of deleted.");
        _guard.Audit("Actions", id.ToString(), new { entity.Code, entity.Name }, null);
        _db.Actions.Remove(entity); await _db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task DeleteFunctionAsync(Guid id, string version, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await _guard.LockAsync(ct);
        var entity = await _db.Functions.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Function not found.");
        AclGuard.CheckVersion(version, entity.Version);
        if (AclGuard.IsManagementFunction(entity.Code) ||
            await _db.ActionInFunctions.AnyAsync(x => x.FunctionId == id, ct) ||
            await _db.Permissions.IgnoreQueryFilters().AnyAsync(x => x.FunctionId == id, ct))
            throw new ConflictException("Referenced functions must be disabled instead of deleted.");
        _guard.Audit("Functions", id.ToString(), new { entity.Code, entity.Name }, null);
        _db.Functions.Remove(entity); await _db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task<FunctionDto> UpdateActionsAsync(Guid id, FunctionActionsRequest request, CancellationToken ct)
    {
        if (request.ActiveActionIds is null || request.ActiveActionIds.Distinct().Count() != request.ActiveActionIds.Count)
            throw new FluentValidation.ValidationException("Action IDs must be a distinct list.");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await _guard.LockAsync(ct);
        var entity = await _db.Functions.Include(x => x.ActionInFunctions).ThenInclude(x => x.Action)
            .SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Function not found.");
        AclGuard.CheckVersion(request.Version, entity.Version);
        if (await _db.Actions.CountAsync(x => request.ActiveActionIds.Contains(x.Id) && x.IsActive, ct) != request.ActiveActionIds.Count)
            throw new FluentValidation.ValidationException("Unknown or inactive action.");
        if (AclGuard.IsManagementFunction(entity.Code) &&
            entity.ActionInFunctions.Any(x => x.IsActive && AclGuard.IsManagementPair(entity.NormalizedCode, x.Action.NormalizedCode) && !request.ActiveActionIds.Contains(x.ActionId)))
            throw new ConflictException("The administration matrix cannot lose existing capabilities.");
        var before = entity.ActionInFunctions.Where(x => x.IsActive).Select(x => x.ActionId).ToArray();
        foreach (var pair in entity.ActionInFunctions)
            pair.IsActive = request.ActiveActionIds.Contains(pair.ActionId);
        foreach (var action in request.ActiveActionIds.Except(entity.ActionInFunctions.Select(x => x.ActionId)))
            entity.ActionInFunctions.Add(new ActionInFunction(action, id));
        entity.Version = Guid.NewGuid().ToString("N");
        _guard.Audit("ActionInFunctions", id.ToString(), before, request.ActiveActionIds);
        await _db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return await GetFunctionAsync(id, ct);
    }
}
