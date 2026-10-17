using System.Text.Json;
namespace Boilerate.Domain.Messaging;

public enum OutboxState
{
    Pending = 1, Processing = 2, Completed = 3, DeadLetter = 4
}
public sealed class OutboxMessage
{
    public Guid MessageId { get; private set; } = Guid.NewGuid();
    public string Type { get; private set; } = "MediaCleanupRequested";
    public int SchemaVersion { get; private set; } = 1;
    public string Payload { get; private set; } = null!;
    public Guid ActorId
    {
        get; private set;
    }
    public string? CorrelationId
    {
        get; private set;
    }
    public DateTime OccurredAt { get; private set; } = DateTime.UtcNow;
    public OutboxState State { get; set; } = OutboxState.Pending;
    public int Attempts
    {
        get; set;
    }
    public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;
    public Guid? LeaseToken
    {
        get; set;
    }
    public DateTime? LeaseUntil
    {
        get; set;
    }
    public string? LastError
    {
        get; set;
    }
    private OutboxMessage()
    {
    }
    public static OutboxMessage Cleanup(Guid mediaId, Guid actor, string? correlation = null) =>
        new()
        {
            Payload = JsonSerializer.Serialize(new MediaCleanupRequested(mediaId)),
            ActorId = actor,
            CorrelationId = correlation
        };
}
public sealed record MediaCleanupRequested(Guid MediaId);
// Stable rows serialize only topology/initialization operations; normal Product/stock writes have their own locks.
public sealed class CommerceLock
{
    public string Name { get; set; } = null!;
    public string Version { get; set; } = Guid.NewGuid().ToString("N");
}