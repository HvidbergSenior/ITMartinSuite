namespace ITMartin.Media.Domain.Entities;

public sealed class MediaIndexEntity
{
    public Guid Id { get; set; }

    public string FilePath { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string MimeType { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public string Sha256 { get; set; } = string.Empty;

    public DateTimeOffset IndexedAt { get; set; }
}