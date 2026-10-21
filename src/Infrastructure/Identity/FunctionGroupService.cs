using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Identity.Roles;
using Boilerate.Domain.Identity;
using Boilerate.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Infrastructure.Identity;

internal sealed class FunctionGroupService(ApplicationDbContext db, AclGuard guard) : IFunctionGroupService
{
    private static FunctionGroupDto Map(FunctionGroup group) => new(group.Id, group.Code, group.Name, group.SortOrder, group.Version);

    private static string Validate(FunctionGroupRequest request)
    {
        var normalized = AclGuard.NormalizeCode(request.Code);
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 100 || request.SortOrder < 0)
            throw new FluentValidation.ValidationException("A group needs a name of 1-100 characters and a nonnegative sort order.");
        return normalized;
    }

    public async Task<List<FunctionGroupDto>> ListAsync(CancellationToken ct)
    {
        await guard.RequireAdminAsync(ct);
        return (await db.FunctionGroups.AsNoTracking().OrderBy(g => g.SortOrder).ThenBy(g => g.Name).ThenBy(g => g.Code).ToListAsync(ct)).Select(Map).ToList();
    }

    public async Task<FunctionGroupDto> CreateAsync(FunctionGroupRequest request, CancellationToken ct)
    {
        var normalized = Validate(request);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await guard.LockAsync(ct);
        if (await db.FunctionGroups.AnyAsync(g => g.NormalizedCode == normalized, ct))
            throw new ConflictException("Group code already exists.");
        var entity = new FunctionGroup { Code = request.Code, NormalizedCode = normalized, Name = request.Name, SortOrder = request.SortOrder };
        db.FunctionGroups.Add(entity);
        guard.Audit("FunctionGroups", entity.Id.ToString(), null, Map(entity));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Map(entity);
    }

    public async Task<FunctionGroupDto> UpdateAsync(Guid id, FunctionGroupRequest request, CancellationToken ct)
    {
        Validate(request);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await guard.LockAsync(ct);
        var entity = await db.FunctionGroups.SingleOrDefaultAsync(g => g.Id == id, ct) ?? throw new NotFoundException("Group not found.");
        AclGuard.CheckVersion(request.Version, entity.Version);
        if (request.Code != entity.Code) throw new ConflictException("Code is immutable.");
        var before = Map(entity);
        entity.Name = request.Name;
        entity.SortOrder = request.SortOrder;
        entity.Version = Guid.NewGuid().ToString("N");
        guard.Audit("FunctionGroups", id.ToString(), before, Map(entity));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Map(entity);
    }

    public async Task DeleteAsync(Guid id, string version, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await guard.LockAsync(ct);
        var entity = await db.FunctionGroups.SingleOrDefaultAsync(g => g.Id == id, ct) ?? throw new NotFoundException("Group not found.");
        AclGuard.CheckVersion(version, entity.Version);
        if (id == FunctionGroup.OtherId || await db.Functions.AnyAsync(f => f.GroupId == id, ct))
            throw new ConflictException("The default group and groups containing functions cannot be deleted.");
        guard.Audit("FunctionGroups", id.ToString(), Map(entity), null);
        db.FunctionGroups.Remove(entity);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
