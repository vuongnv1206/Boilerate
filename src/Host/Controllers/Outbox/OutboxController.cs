using Boilerate.Application.Outbox;
using Boilerate.Domain.Messaging;
using Boilerate.Infrastructure.Auth.Permissions;
using Boilerate.Shared.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Boilerate.Host.Controllers.Outbox;

[Route("api/outbox")]
public sealed class OutboxController : BaseApiController
{
    [HttpGet, MustHavePermission(AppAction.View, AppFunction.Outbox)]
    public Task<OutboxMessagePageDto> List(CancellationToken ct, OutboxState? state = null, int page = 1, int size = 20) =>
        Mediator.Send(new GetOutboxMessagesRequest(state, page, size), ct);

    [HttpPost("{id:guid}/replay"), MustHavePermission(AppAction.Replay, AppFunction.Outbox)]
    public Task<bool> Replay(Guid id, CancellationToken ct) =>
        Mediator.Send(new ReplayOutboxMessageRequest(id), ct);
}
