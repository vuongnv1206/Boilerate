using Boilerate.Domain.Catalog;

namespace Boilerate.Application.Ordering;

public sealed record OverviewWorkItem(Guid Id, Guid OrderId, string OrderNumber, string? Name, string? Sku,
    int Status, DateTime CreatedAtUtc);
public sealed record OverviewWorkGroup(string Key, string Kind, int Total, List<OverviewWorkItem> Items);
public sealed record OverviewWorkDto(DateTime GeneratedAtUtc, List<OverviewWorkGroup> Groups);
public sealed record OverviewFinanceDay(DateOnly Date, string Received, string Refunded, string Net);
public sealed record OverviewFinanceDto(DateTime GeneratedAtUtc, DateOnly From, DateOnly To, string TimeZone,
    string Currency, string Received, string Refunded, string Net, List<OverviewFinanceDay> Days);
public sealed record OverviewStockItem(Guid ProductId, Guid VariantId, string ProductName, string Sku,
    ProductFulfillmentKind FulfillmentKind, int Available);
public sealed record OverviewStockDto(DateTime GeneratedAtUtc, int Threshold, int PageNumber, int PageSize,
    int Total, List<OverviewStockItem> Items);

public interface IAdminOverviewService
{
    Task<OverviewWorkDto> WorkAsync(CancellationToken ct);
    Task<OverviewFinanceDto> FinanceAsync(DateOnly? from, DateOnly? to, CancellationToken ct);
    Task<OverviewStockDto> StockAsync(int threshold, int page, int size, CancellationToken ct);
}
