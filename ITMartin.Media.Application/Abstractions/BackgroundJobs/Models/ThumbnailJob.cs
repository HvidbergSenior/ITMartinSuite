namespace ITMartin.Media.Application.Abstractions.BackgroundJobs.Models;

public sealed class ThumbnailJob
{
    public Guid MediaId { get; set; }

    public string FilePath { get; set; } = string.Empty;
}