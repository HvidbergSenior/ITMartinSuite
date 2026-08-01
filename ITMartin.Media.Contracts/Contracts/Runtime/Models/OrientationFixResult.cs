namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public sealed class OrientationFixResult
{
    public int PhotosChecked { get; init; }
    public int PhotosRotated { get; init; }
    public int RemainingCount { get; init; }
}
