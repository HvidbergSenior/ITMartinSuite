namespace ITMartin.Documents.Models;

// Plain result of parsing a .docx - deliberately not an EF entity, so any
// consuming app (RedigerDokument, eventually AeroMedRecord) maps this into
// its own Section-shaped entity/DbContext rather than sharing a schema.
public sealed class ParsedDocumentSection
{
    public int SortOrder { get; set; }
    public string? Heading { get; set; }
    public string Text { get; set; } = string.Empty;
}
