namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public sealed class ImageTaggingResult
{
    public int TaggedCount { get; init; }
    public int AlreadyTaggedCount { get; init; }
    public int TotalImages { get; init; }

    /// <summary>
    /// How many untagged images were left over after this run's MaxCallsPerRun
    /// cap - non-zero means another click will continue where this left off.
    /// </summary>
    public int RemainingCount { get; init; }
}
