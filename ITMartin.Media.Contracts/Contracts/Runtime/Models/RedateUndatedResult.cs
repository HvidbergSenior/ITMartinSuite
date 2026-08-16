namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public sealed class RedateUndatedResult
{
    public int Checked { get; init; }
    public int Moved { get; init; }
    public int StillUndated { get; init; }
}
