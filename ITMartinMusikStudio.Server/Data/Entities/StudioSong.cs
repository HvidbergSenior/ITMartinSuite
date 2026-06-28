namespace ITMartinMusikStudio.Server.Data.Entities;

public class StudioSong
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = "";
    public string Title { get; set; } = "";
    public string Artist { get; set; } = "";
    public string SourceFile { get; set; } = "";
    public string MusicKey { get; set; } = "Am";
    public int? Tempo { get; set; }
    public string Lyrics { get; set; } = "";
    public string ChordChart { get; set; } = "";
    public string Notes { get; set; } = "";
    public string FingerpickPattern { get; set; } = "";
    public string StrumPattern { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
