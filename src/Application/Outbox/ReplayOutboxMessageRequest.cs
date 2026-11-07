using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Domain.Auditing;
using Boilerate.Domain.Messaging;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Outbox;

public sealed record ReplayOutboxMessageRequest(Guid MessageId) : IRequest<bool>;

public sealed class ReplayOutboxMessageRequestValidator : CustomValidator<ReplayOutboxMessageRequest>
{
    public ReplayOutboxMessageRequestValidator()
    {
        RuleFor(x => x.MessageId).NotEmpty();
    }
}

public sealed class ReplayOutboxMessageRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work,
    ICurrentUser actor) : IRequestHandler<ReplayOutboxMessageRequest, bool>
{
    public Task<bool> Handle(ReplayOutboxMessageRequest request, CancellationToken ct) =>
        work.ExecuteAsync(async () =>
        {
            var message = await db.OutboxMessages.SingleOrDefaultAsync(m => m.MessageId == request.MessageId, ct)
                ?? throw new NotFoundException("Message not found.");
            if (message.State != OutboxState.DeadLetter)
            {
                throw new ConflictException("Only dead-letter messages can be replayed.");
            }

            int changed = await db.OutboxMessages
                .Where(m => m.MessageId == request.MessageId && m.State == OutboxState.DeadLetter)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(m => m.State, OutboxState.Pending)
                        .SetProperty(m => m.Attempts, 0)
                        .SetProperty(m => m.NextAttemptAt, DateTime.UtcNow)
                        .SetProperty(m => m.LeaseToken, (Guid?)null)
                        .SetProperty(m => m.LeaseUntil, (DateTime?)null),
                    ct);
            if (changed != 1)
            {
                throw new ConflictException("Message was already replayed.");
            }

            db.Trails.Add(new Trail
            {
                UserId = actor.GetUserId(),
                TableName = "OutboxMessages",
                PrimaryKey = request.MessageId.ToString(),
                Type = TrailType.Update,
                DateTime = DateTime.UtcNow,
                OldValues = "{\"State\":\"DeadLetter\"}",
                NewValues = "{\"State\":\"Pending\",\"Source\":\"OperatorReplay\"}",
            });
            return true;
        }, ct);
}
