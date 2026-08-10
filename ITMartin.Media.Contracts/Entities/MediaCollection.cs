namespace ITMartin.Media.Contracts.Entities;

public class MediaCollection
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    // Person/Yearbook/Trip/Tradition/BestShot - lets the gallery group Yearbook
    // and Person separately from the rest (shown as "Eksempel" cards) instead of
    // lumping every SmartFolders kind into one undifferentiated row.
    public string Type { get; set; } = "";
    public List<string> FilePaths { get; set; } = [];
}
