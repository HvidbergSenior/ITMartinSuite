namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public sealed class EnhancementOperation
{
    public required string Name { get; init; }

    public DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset CompletedAt { get; set; }

    public bool Success { get; set; }

    public string? Metadata { get; set; }
}