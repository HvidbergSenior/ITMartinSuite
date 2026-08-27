namespace ITMartinRedigerDokument.Server.Data.Entities;

public sealed class Comment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SectionId { get; set; }
    public Guid GroupId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DocumentSection Section { get; set; } = null!;
}
