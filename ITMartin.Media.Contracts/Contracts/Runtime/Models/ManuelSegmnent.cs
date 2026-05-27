namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public sealed class ManualSegment
{
    public required string Name
    {
        get;
        init;
    }

    public required TimeSpan Start
    {
        get;
        init;
    }

    public required TimeSpan End
    {
        get;
        init;
    }
}