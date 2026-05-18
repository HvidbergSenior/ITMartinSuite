// File: ITMartin.Media.Application/Queues/MessageEnvelope.cs

namespace ITMartin.Media.Application.Abstractions.Queues;

public sealed class MessageEnvelope
{
    public Guid MessageId { get; init; }

    public required string MessageType { get; init; }

    public required string Payload { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }
        = DateTimeOffset.UtcNow;

    public int RetryCount { get; set; }
}