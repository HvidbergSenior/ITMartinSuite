namespace ITMartin.Media.Application.Queues.Models;

public sealed class QueueMessage
{
    public Guid Id { get; set; }

    public string Queue { get; set; }
        = string.Empty;

    public string Payload { get; set; }
        = string.Empty;

    public string Type { get; set; }
        = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}