using Boilerate.Application.Catalog;
using Boilerate.Domain.Messaging;
using Boilerate.Infrastructure.Auth.Permissions;
using Boilerate.Infrastructure.Persistence.Context;
using Boilerate.Shared.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Boilerate.Host.Controllers.Catalog;

[Route("api/outbox")]
public sealed class OutboxController(ApplicationDbContext db) : BaseApiController
{
    [HttpGet, MustHavePermission(AppAction.View, AppFunction.Outbox)]
    public async Task<IActionResult> List(CancellationToken ct, OutboxState? state = null, int page = 1, int size = 20)
    {
        if (page < 1 || size is < 1 or > 100 || (long)(page - 1) * size > int.MaxValue || (state.HasValue && !Enum.IsDefined(state.Value)))
            return BadRequest();
        var q = db.OutboxMessages.AsNoTracking().Where(m => !state.HasValue || m.State == state);
        return Ok(new
        {
            total = await q.CountAsync(ct),
            page,
            size,
            items = await q.OrderBy(m => m.OccurredAt).ThenBy(m => m.MessageId).Skip((page - 1) * size).Take(size)
            .Select(m => new { m.MessageId, m.Type, m.SchemaVersion, m.OccurredAt, m.State, m.Attempts, m.NextAttemptAt, m.LeaseUntil, m.LastError }).ToListAsync(ct)
        });
    }
    [HttpPost("{id:guid}/replay"), MustHavePermission(AppAction.Replay, AppFunction.Outbox)]
    public Task<bool> Replay(Guid id, CancellationToken ct) => Mediator.Send(new ReplayOutbox(id), ct);
}