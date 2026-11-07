using Boilerate.Application.Overview;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Boilerate.Host.Controllers.Overview;

[Authorize, Route("api/admin/overview"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminOverviewController : BaseApiController
{
    [HttpGet("work")]
    public Task<OverviewWorkDto> Work(CancellationToken ct) =>
        Mediator.Send(new GetOverviewWorkRequest(), ct);

    [HttpGet("finance")]
    public Task<OverviewFinanceDto> Finance(CancellationToken ct, DateOnly? from = null, DateOnly? to = null) =>
        Mediator.Send(new GetOverviewFinanceRequest(from, to), ct);

    [HttpGet("stock")]
    public Task<OverviewStockDto> Stock(CancellationToken ct, int threshold = 5, int page = 1, int size = 10) =>
        Mediator.Send(new GetOverviewStockRequest(threshold, page, size), ct);
}
