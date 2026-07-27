namespace ITMartinMusic.Server.Data.Entities;

public sealed class SongComment
{
    public int      Id        { get; set; }
    public string   SongKey   { get; set; } = "";
    public Guid?    SongVersionId { get; set; } // null = general comment, not tied to a specific take
    public string   Name      { get; set; } = "";
    public string   Text      { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
