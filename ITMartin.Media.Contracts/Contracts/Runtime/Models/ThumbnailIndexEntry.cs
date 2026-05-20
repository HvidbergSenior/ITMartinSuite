namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public sealed class ThumbnailIndexEntry
{
    public Guid MediaId { get; set; }

    public string ThumbnailPath { get; set; }
        = string.Empty;

    public int Width { get; set; }

    public int Height { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}