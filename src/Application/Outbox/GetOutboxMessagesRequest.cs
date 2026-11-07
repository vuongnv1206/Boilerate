using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Domain.Messaging;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Outbox;

public sealed record GetOutboxMessagesRequest(OutboxState? State = null, int Page = 1, int Size = 20)
    : IRequest<OutboxMessagePageDto>;

public sealed class GetOutboxMessagesRequestValidator : CustomValidator<GetOutboxMessagesRequest>
{
    public GetOutboxMessagesRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.Size).InclusiveBetween(1, 100);
        RuleFor(x => x)
            .Must(x => (long)(x.Page - 1) * x.Size <= int.MaxValue)
            .WithMessage("Pagination offset overflow.");
        RuleFor(x => x.State)
            .Must(state => !state.HasValue || Enum.IsDefined(state.Value))
            .WithMessage("Invalid outbox state.");
    }
}

public sealed class GetOutboxMessagesRequestHandler(IApplicationDbContext db)
    : IRequestHandler<GetOutboxMessagesRequest, OutboxMessagePageDto>
{
    public async Task<OutboxMessagePageDto> Handle(GetOutboxMessagesRequest request, CancellationToken ct)
    {
        var query = db.OutboxMessages
            .AsNoTracking()
            .Where(m => !request.State.HasValue || m.State == request.State);
        int total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(m => m.OccurredAt)
            .ThenBy(m => m.MessageId)
            .Skip((request.Page - 1) * request.Size)
            .Take(request.Size)
            .Select(m => new OutboxMessageDto(
                m.MessageId,
                m.Type,
                m.SchemaVersion,
                m.OccurredAt,
                m.State,
                m.Attempts,
                m.NextAttemptAt,
                m.LeaseUntil,
                m.LastError))
            .ToListAsync(ct);
        return new(total, request.Page, request.Size, items);
    }
}
