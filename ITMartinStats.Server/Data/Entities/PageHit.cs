namespace ITMartinStats.Server.Data.Entities;

public class PageHit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Host { get; set; } = "";
    public string Path { get; set; } = "";
    public string Title { get; set; } = "";
    public string Referrer { get; set; } = "";
    public string Device { get; set; } = "";
    public string VisitorId { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
