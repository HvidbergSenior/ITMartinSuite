namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public sealed class EnhancedMediaItem
{
    public required string OriginalPath { get; init; }

    public required string NormalizedPath { get; set; }

    public required MediaKind MediaKind { get; init; }

    public string? CurrentWorkingPath { get; set; }

    public string? AudioWorkingPath { get; set; }

    public IList<EnhancementOperation> Operations { get; init; }
        = [];

    public bool Failed { get; set; }

    public string? FailureReason { get; set; }
}