namespace ITMartinAeroMedRecord.Server.Data.Entities;

// One editable chunk of a document's extracted text (roughly one heading or
// paragraph from the original). Editing happens per-section rather than as
// one giant text blob - keeps concurrent edits from two teammates cheap to
// reason about (each section is its own row, so editing different sections
// at once never conflicts) and keeps the diff/version history meaningful.
public sealed class DocumentSection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public Guid GroupId { get; set; }
    public int SortOrder { get; set; }
    public string? Heading { get; set; }
    public string Text { get; set; } = string.Empty;
    public string LastEditedByName { get; set; } = string.Empty;
    public DateTime LastEditedAt { get; set; } = DateTime.UtcNow;

    public Document Document { get; set; } = null!;
    public List<SectionVersion> Versions { get; set; } = [];
    public List<Comment> Comments { get; set; } = [];
    public List<Reference> References { get; set; } = [];
}
