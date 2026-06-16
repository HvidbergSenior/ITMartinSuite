namespace ITMartinAdhd.Domain.Entities;

public sealed class StoredItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Location { get; set; } = "";
    public string? Notes { get; set; }
    public DateTime StoredAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
