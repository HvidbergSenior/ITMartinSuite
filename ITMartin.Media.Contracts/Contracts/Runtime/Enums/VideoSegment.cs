namespace ITMartin.Media.Contracts.Contracts.Runtime.Enums;

public sealed class VideoSegment
{
    public TimeSpan Start
    {
        get;
        set;
    }

    public TimeSpan End
    {
        get;
        set;
    }

    public TimeSpan Duration =>
        End - Start;
}