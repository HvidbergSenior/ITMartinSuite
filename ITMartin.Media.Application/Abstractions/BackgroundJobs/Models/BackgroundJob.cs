namespace ITMartin.Media.Application.Abstractions.BackgroundJobs.Models;

public sealed class BackgroundJob
{
    public Guid Id { get; set; }

    public string Queue { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string Payload { get; set; } = string.Empty;

    public int RetryCount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public string Status { get; set; } = string.Empty;
}