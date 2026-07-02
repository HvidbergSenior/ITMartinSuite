namespace ITMartinMusic.Server.Data.Entities;

public sealed class PracticeEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SongId { get; set; }
    public string Notes { get; set; } = string.Empty;
    public int Rating { get; set; } = 3;
    public DateTime LoggedAt { get; set; } = DateTime.UtcNow;
    public Song Song { get; set; } = null!;
}
