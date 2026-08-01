namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public sealed class BestShotResult
{
    public int BurstsFound { get; init; }
    public int PhotosPicked { get; init; }
    public string FolderPath { get; init; } = "";
}
