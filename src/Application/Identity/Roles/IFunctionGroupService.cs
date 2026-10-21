using Boilerate.Application.Common.Interfaces;

namespace Boilerate.Application.Identity.Roles;

public sealed record FunctionGroupDto(Guid Id, string Code, string Name, int SortOrder, string Version);

public sealed class FunctionGroupRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public string? Version { get; set; }
}

public interface IFunctionGroupService : IScopedService
{
    Task<List<FunctionGroupDto>> ListAsync(CancellationToken ct);
    Task<FunctionGroupDto> CreateAsync(FunctionGroupRequest request, CancellationToken ct);
    Task<FunctionGroupDto> UpdateAsync(Guid id, FunctionGroupRequest request, CancellationToken ct);
    Task DeleteAsync(Guid id, string version, CancellationToken ct);
}
