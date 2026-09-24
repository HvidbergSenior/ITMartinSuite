namespace ITMartinStrategi.Server.Data.Entities;

/// <summary>One thing you can look up in a game's library: a wiki page, or one row of a
/// wiki table (an artifact, a technology, a civilization …). Text holds "Field: value"
/// lines for table rows and plain paragraphs for pages. Rebuilt by WikiLibrary.</summary>
public sealed class WikiEntry
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public string Title { get; set; } = "";
    public string Kind { get; set; } = "";      // "Artifacts", "Technology", "Page" …
    public string Text { get; set; } = "";
    public string Url { get; set; } = "";
    public string Icon { get; set; } = "";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
