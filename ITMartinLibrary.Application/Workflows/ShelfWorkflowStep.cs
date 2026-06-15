namespace ITMartinLibrary.Application.Workflows;

public sealed class ShelfWorkflowStep
{
    public required string Name { get; init; }

    public required bool Success { get; init; }

    public required TimeSpan Duration { get; init; }

    public string? Error { get; init; }
}
