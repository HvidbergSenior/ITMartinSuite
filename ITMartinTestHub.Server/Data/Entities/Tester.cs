namespace ITMartinTestHub.Server.Data.Entities;

public sealed class Tester
{
    public Guid     Id        { get; set; } = Guid.NewGuid();
    public string   Name      { get; set; } = "";
    public string   Color     { get; set; } = "#6366f1";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
