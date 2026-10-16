using Boilerate.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Boilerate.Host.Controllers;

[ApiController]
[AllowAnonymous]
public sealed class HealthController(ApplicationDbContext db, ILogger<HealthController> logger) : ControllerBase
{
    [HttpGet("/health/live")]
    public IActionResult Live() => Ok(new { Status = "Alive" });

    [HttpGet("/health/ready")]
    public async Task<IActionResult> Ready(CancellationToken ct)
    {
        try
        {
            if (await db.Database.CanConnectAsync(ct) && !(await db.Database.GetPendingMigrationsAsync(ct)).Any())
            {
                return Ok(new { Status = "Ready" });
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // Readiness must fail closed even for unexpected persistence failures; details stay in server logs.
            logger.LogWarning(ex, "Database readiness check failed.");
        }

        return StatusCode(503, new { Status = "NotReady" });
    }
}
