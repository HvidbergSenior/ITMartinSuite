namespace ITMartinRedigerDokument.Server.Data.Entities;

// Snapshot of a DocumentSection's text taken right before each save - nothing
// a teammate writes is ever silently lost, and it's possible to see who
// changed what, when.
public sealed class SectionVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SectionId { get; set; }
    public string Text { get; set; } = string.Empty;
    public string EditedByName { get; set; } = string.Empty;
    public DateTime EditedAt { get; set; } = DateTime.UtcNow;

    public DocumentSection Section { get; set; } = null!;
}
