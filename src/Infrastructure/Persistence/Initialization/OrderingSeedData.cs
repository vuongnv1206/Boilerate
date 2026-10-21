using System.Text.Json;
using System.Text.Json.Serialization;
using Boilerate.Domain.Ordering;

namespace Boilerate.Infrastructure.Persistence.Initialization;

internal sealed record OrderingSeedData(OrderScenarioSeed[] Scenarios)
{
    internal static async Task<OrderingSeedData> ReadAsync(CancellationToken cancellationToken)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "SeedData", "ordering.json");
        await using var file = File.OpenRead(path);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            RespectRequiredConstructorParameters = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            Converters = { new JsonStringEnumConverter<OrderPaymentMethod>(allowIntegerValues: false) }
        };
        var data = await JsonSerializer.DeserializeAsync<OrderingSeedData>(file, options, cancellationToken)
            ?? throw new InvalidOperationException("SeedData/ordering.json is empty.");
        data.Validate();
        return data;
    }

    private void Validate()
    {
        if (Scenarios is not { Length: > 0 } || Scenarios.Any(s => s is null || string.IsNullOrWhiteSpace(s.Key) ||
            s.Lines is not { Length: > 0 } || s.Lines.Any(line => line is null || string.IsNullOrWhiteSpace(line.Sku) || line.Quantity is < 1 or > 50) ||
            !Enum.IsDefined(s.PaymentMethod) || string.IsNullOrWhiteSpace(s.Action)))
            throw new InvalidOperationException("ordering.json contains an invalid scenario.");
        if (Scenarios.Select(s => s.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Scenarios.Length)
            throw new InvalidOperationException("ordering.json contains duplicate scenario keys.");
    }
}

internal sealed record OrderScenarioSeed(string Key, OrderPaymentMethod PaymentMethod, OrderLineSeed[] Lines, string Action);
internal sealed record OrderLineSeed(string Sku, int Quantity);
