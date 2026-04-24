namespace ITMartinImageProcessor.Domain.Entities;

public class ImageFile
{
    public string Path { get; set; } = default!;
    public DateTime CreatedAt { get; set; }

    public bool IsRecent()
        => CreatedAt > DateTime.UtcNow.AddDays(-7);
}