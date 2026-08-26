namespace ITMartinAeroMedRecord.Server.Data.Entities;

public sealed class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Set when the message carries an image/screenshot attachment instead
    // of (or alongside) text - stored on disk under ChatImagesRoot/{GroupId},
    // served back via the /chat-image/{id} endpoint.
    public string? ImageFileName { get; set; }
    public string? ImageContentType { get; set; }

    public Group Group { get; set; } = null!;
}
