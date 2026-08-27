namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public sealed class TripFolderResult
{
    public required string Name { get; init; }
    public DateTime Start { get; init; }
    public DateTime End { get; init; }
    public int FileCount { get; init; }
    public required string FolderPath { get; init; }
}

public sealed class PersonFolderResult
{
    public required string Name { get; init; }
    public int FileCount { get; init; }
    public required string FolderPath { get; init; }
}

public sealed class YearbookResult
{
    public int Year { get; init; }
    public int PhotoCount { get; init; }
    public required string FolderPath { get; init; }
    public required string HtmlPath { get; init; }
}

public sealed class SimilarSceneResult
{
    public required string Name { get; init; }
    public int FileCount { get; init; }
    public required string FolderPath { get; init; }
}

public sealed class UndatedEstimateResult
{
    public int Processed { get; init; }
    public int Moved { get; init; }
    public int LowConfidenceLeftInPlace { get; init; }
    public int NoUsableClueLeftInPlace { get; init; }
    public int RemainingUnprocessed { get; init; }
}
