using System.ComponentModel.DataAnnotations;
namespace Boilerate.Infrastructure.Commerce;

public sealed class CommerceSettings
{
    [Range(1, 10)] public int MaximumOptions { get; set; } = 3;
    [Range(1, 1000)] public int MaximumVariants { get; set; } = 200;
    [Range(1, 60)] public int WorkerIntervalSeconds { get; set; } = 5;
    [Range(5, 600)] public int LeaseSeconds { get; set; } = 120;
    [Range(1, 20)] public int MaximumAttempts { get; set; } = 8;
    public bool WorkerEnabled { get; set; } = true;
}