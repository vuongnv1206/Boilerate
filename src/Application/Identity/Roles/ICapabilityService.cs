using Boilerate.Application.Common.Interfaces;

namespace Boilerate.Application.Identity.Roles;

public class CapabilityRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string? Version { get; set; }
}

public sealed class FunctionRequest : CapabilityRequest
{
    public Guid? GroupId { get; set; }
}

public sealed class FunctionActionsRequest
{
    public List<Guid> ActiveActionIds { get; set; } = new();
    public string Version { get; set; } = string.Empty;
}

public interface ICapabilityService : IScopedService
{
    Task<List<ActionDto>> GetActionsAsync(CancellationToken ct);
    Task<ActionDto> GetActionAsync(Guid id, CancellationToken ct);
    Task<ActionDto> CreateActionAsync(CapabilityRequest request, CancellationToken ct);
    Task<ActionDto> UpdateActionAsync(Guid id, CapabilityRequest request, CancellationToken ct);
    Task DeleteActionAsync(Guid id, string version, CancellationToken ct);
    Task<List<FunctionDto>> GetFunctionsAsync(CancellationToken ct);
    Task<FunctionDto> GetFunctionAsync(Guid id, CancellationToken ct);
    Task<FunctionDto> CreateFunctionAsync(FunctionRequest request, CancellationToken ct);
    Task<FunctionDto> UpdateFunctionAsync(Guid id, FunctionRequest request, CancellationToken ct);
    Task DeleteFunctionAsync(Guid id, string version, CancellationToken ct);
    Task<FunctionDto> UpdateActionsAsync(Guid id, FunctionActionsRequest request, CancellationToken ct);
}
