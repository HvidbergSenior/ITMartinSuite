namespace ITMartinUret.Server.Data.Entities;

public class Attachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PostId { get; set; }
    public Post Post { get; set; } = null!;

    public string FileName { get; set; } = "";
    public string StoredPath { get; set; } = "";

    // Poster explicitly confirmed they redacted other people's personal data before this upload was accepted.
    public DateTime RedactionAckAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // AI-generated factual resume of the document (e.g. an emailed complaint response) — clearly
    // labelled as AI-generated wherever shown, never presented as the poster's own words.
    public string? LegalSummary { get; set; }
    public DateTime? SummaryGeneratedAt { get; set; }
}
