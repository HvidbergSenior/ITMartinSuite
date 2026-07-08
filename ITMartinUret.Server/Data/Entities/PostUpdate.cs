namespace ITMartinUret.Server.Data.Entities;

public class PostUpdate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PostId { get; set; }
    public Post Post { get; set; } = null!;

    public string Body { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
