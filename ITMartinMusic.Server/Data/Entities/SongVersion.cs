namespace ITMartinMusic.Server.Data.Entities;

// One row per published take, discovered by scanning myversions/ (see
// Songs.razor ScanSongs()) rather than written directly by MusikStudio -
// the two apps only share the filesystem, not a database.
public sealed class SongVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SongKey { get; set; } = "";
    public string FileName { get; set; } = "";
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public bool IsVisible { get; set; } = true;
}
