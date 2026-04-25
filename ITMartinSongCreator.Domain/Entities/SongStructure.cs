namespace ITMartinSongCreator.Domain.Entities;

public class SongStructure
{
    public string Name { get; set; } = string.Empty;
    public List<string> Sections { get; set; } = new();
    public Dictionary<string, string> Suggestions { get; set; } = new();

    // Per-section variation
    public Dictionary<string, Func<List<string>, List<string>>> SectionVariations { get; set; } = new();

    // Bass string per section
    public Dictionary<string, string> SectionBass { get; set; } = new();
}