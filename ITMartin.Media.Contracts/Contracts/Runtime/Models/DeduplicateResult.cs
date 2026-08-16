namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public sealed class DeduplicateResult
{
    public int Checked { get; init; }
    public int Deleted { get; init; }
}
