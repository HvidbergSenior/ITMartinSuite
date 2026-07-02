namespace ITMartinFamily.Domain.Entities;

public sealed class FamilyStoredItem
{
    public Guid     Id        { get; set; } = Guid.NewGuid();
    public Guid     FamilyId  { get; set; }
    public string   Name      { get; set; } = "";
    public string   Location  { get; set; } = "";
    public string?  Notes     { get; set; }
    public string?  PhotoPath { get; set; }
    public DateTime StoredAt  { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
