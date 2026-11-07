using Boilerate.Domain.Messaging;

namespace Boilerate.Application.Outbox;

public sealed record OutboxMessageDto(
    Guid MessageId,
    string Type,
    int SchemaVersion,
    DateTime OccurredAt,
    OutboxState State,
    int Attempts,
    DateTime NextAttemptAt,
    DateTime? LeaseUntil,
    string? LastError);

public sealed record OutboxMessagePageDto(
    int Total,
    int Page,
    int Size,
    List<OutboxMessageDto> Items);
