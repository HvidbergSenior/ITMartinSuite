namespace ITMartinLiveGallery.Server;

public class LiveEventInfo
{
    public string Slug { get; set; } = "";
    public string Pin { get; set; } = "";
    public string Title { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class EventPhoto
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Slug { get; set; } = "";
    public string Filename { get; set; } = "";
    public string ThumbFilename { get; set; } = "";
    public bool IsVideo { get; set; }
    public string? UploaderName { get; set; }
    public DateTime UploadedAt { get; set; }
}
