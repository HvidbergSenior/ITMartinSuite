namespace ITMartinRedigerDokument.Server.Data.Entities;

// The original, uploaded file - stored as-is and never modified. Everyone's
// real day-to-day interaction happens through this document's DocumentSections
// instead (extracted text, editable in the browser, no Word/Office needed);
// this stays around purely as the immutable source of truth and archival
// download, not the primary way anyone reads or edits the content.
public sealed class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string UploadedByName { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public Group Group { get; set; } = null!;
    public List<DocumentSection> Sections { get; set; } = [];
}
