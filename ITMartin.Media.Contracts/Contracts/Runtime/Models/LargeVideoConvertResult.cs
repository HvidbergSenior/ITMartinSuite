namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public sealed class LargeVideoConvertResult
{
    public required int TotalDeferred { get; init; }

    public required int Converted { get; init; }

    public required int Failed { get; init; }
}
