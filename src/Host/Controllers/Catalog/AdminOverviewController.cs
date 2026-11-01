using Boilerate.Application.Ordering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Boilerate.Host.Controllers.Catalog;

[Authorize, Route("api/admin/overview"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminOverviewController(IAdminOverviewService overview) : BaseApiController
{
    [HttpGet("work")]
    public Task<OverviewWorkDto> Work(CancellationToken ct) => overview.WorkAsync(ct);

    [HttpGet("finance")]
    public Task<OverviewFinanceDto> Finance(CancellationToken ct, DateOnly? from = null, DateOnly? to = null) =>
        overview.FinanceAsync(from, to, ct);

    [HttpGet("stock")]
    public Task<OverviewStockDto> Stock(CancellationToken ct, int threshold = 5, int page = 1, int size = 10) =>
        overview.StockAsync(threshold, page, size, ct);
}
