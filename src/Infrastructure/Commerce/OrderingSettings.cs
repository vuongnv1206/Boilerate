using System.ComponentModel.DataAnnotations;

namespace Boilerate.Infrastructure.Commerce;

public sealed class OrderingSettings
{
    [Range(1, 1440)] public int ReservationMinutes { get; set; } = 30;
    [Range(typeof(decimal), "0", "9999999999999999")] public decimal FlatShippingAmount { get; set; }
    [Range(1, 60)] public int WorkerIntervalSeconds { get; set; } = 15;
    [Range(1, 100)] public int ExpirationBatchSize { get; set; } = 50;
    [Range(1, 120)] public int RenewalClaimMinutes { get; set; } = 15;
}
