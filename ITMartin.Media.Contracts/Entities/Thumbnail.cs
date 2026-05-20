namespace ITMartin.Media.Contracts.Entities;

public sealed class Thumbnail
{
    public Guid Id { get; set; }

    public Guid MediaId { get; set; }

    public string ThumbnailPath { get; set; } = string.Empty;

    public int Width { get; set; }

    public int Height { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}