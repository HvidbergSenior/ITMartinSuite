namespace ITMartinTestHub.Server.Data.Entities;

public sealed class AppEntry
{
    public Guid   Id          { get; set; } = Guid.NewGuid();
    public string Name        { get; set; } = "";
    public string Url         { get; set; } = "";
    public string Icon        { get; set; } = "🔷";
    public string? Description { get; set; }
    public int    SortOrder   { get; set; }

    public List<TestStep> Steps { get; set; } = [];
}
