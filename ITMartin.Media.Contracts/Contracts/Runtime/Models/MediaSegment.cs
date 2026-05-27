namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public sealed class MediaSegment
{
    public Guid Id { get; set; } =
        Guid.NewGuid();

    public TimeSpan Start { get; set; }

    public TimeSpan End { get; set; }

    public double DurationSeconds =>
        (End - Start).TotalSeconds;

    public string? ThumbnailPath { get; set; }

    public string? SegmentPath { get; set; }

    public string? Description { get; set; }

    public bool HasBlackFrameStart { get; set; }

    public bool HasBlackFrameEnd { get; set; }
}