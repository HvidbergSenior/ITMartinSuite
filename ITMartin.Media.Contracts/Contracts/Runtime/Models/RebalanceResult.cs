namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public sealed class RebalanceResult
{
    public int FilesChecked { get; init; }
    public int FilesMoved { get; init; }
    public int YearsProcessed { get; init; }
}
