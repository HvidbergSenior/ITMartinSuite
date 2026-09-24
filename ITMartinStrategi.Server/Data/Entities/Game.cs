namespace ITMartinStrategi.Server.Data.Entities;

/// <summary>A game Martin plays. Topics are the suggested things to draft a
/// guide about, one per line as "section|topic" (e.g. "valg|Oligarki").</summary>
public sealed class Game
{
    public int Id { get; set; }
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public string Platform { get; set; } = "";          // "PC" | "Digitalt brætspil"
    public string Version { get; set; } = "";           // patch/expansions the guides are written for
    public string Icon { get; set; } = "🎲";
    public string Topics { get; set; } = "";
    public int SortOrder { get; set; }
    public List<Guide> Guides { get; set; } = [];

    public IEnumerable<(string Section, string Topic)> TopicList() =>
        Topics.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(l => l.Split('|', 2))
            .Where(p => p.Length == 2)
            .Select(p => (p[0].Trim(), p[1].Trim()));
}
