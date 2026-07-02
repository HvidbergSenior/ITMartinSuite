namespace ITMartinMusic.Server.Data.Entities;

public sealed class Song
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public int? Tempo { get; set; }
    public string ChordChart { get; set; } = string.Empty;
    public string Lyrics { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<PracticeEntry> PracticeEntries { get; set; } = [];
}
