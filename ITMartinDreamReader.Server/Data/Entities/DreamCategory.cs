namespace ITMartinDreamReader.Server.Data.Entities;

public class DreamCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Emoji { get; set; } = "";
    public string Layer { get; set; } = ""; // "Who", "Where", "Doing", "Feeling"

    public List<DreamEntry> Entries { get; set; } = [];
}
